using System.Buffers.Binary;
using FluentAssertions;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Exceptions;
using Modbus.Core.Protocol.Tcp;

namespace Modbus.Core.Tests.Protocol.Tcp;

public class ModbusTcpFrameParserTests
{
    private readonly ModbusTcpFrameParser _parser = new();

    /// <summary>
    /// Builds a valid TCP read response: MBAP(7) + FC(1) + ByteCount(1) + Data(N*2)
    /// </summary>
    private static byte[] BuildReadResponse(byte unitId, byte fc, ushort[] values)
    {
        int byteCount = values.Length * 2;
        int pduLen = 2 + byteCount;  // FC + ByteCount + Data
        var frame = new byte[7 + pduLen];

        // MBAP header
        BinaryPrimitives.WriteUInt16BigEndian(frame, 0x0001);       // Transaction ID
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0);  // Protocol ID
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(1 + pduLen)); // Length
        frame[6] = unitId;

        // PDU
        frame[7] = fc;
        frame[8] = (byte)byteCount;
        for (int i = 0; i < values.Length; i++)
            BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(9 + i * 2), values[i]);

        return frame;
    }

    /// <summary>
    /// Builds a TCP error response: MBAP(7) + FC|0x80(1) + ExceptionCode(1)
    /// </summary>
    private static byte[] BuildErrorResponse(byte unitId, byte fc, byte exceptionCode)
    {
        var frame = new byte[9];
        BinaryPrimitives.WriteUInt16BigEndian(frame, 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), 3); // Length: UnitId + FC + ExCode
        frame[6] = unitId;
        frame[7] = (byte)(fc | 0x80);
        frame[8] = exceptionCode;
        return frame;
    }

    // ── ParseReadRegisters ───────────────────────────────────────────────────

    [Fact]
    public void ParseReadRegisters_TwoRegisters_ReturnsParsedValues()
    {
        var response = BuildReadResponse(255, 0x03, [0x1234, 0x5678]);

        var result = _parser.ParseReadRegisters(response);

        result.Should().HaveCount(2);
        result[0].Should().Be(0x1234);
        result[1].Should().Be(0x5678);
    }

    [Fact]
    public void ParseReadRegisters_SingleRegister_ReturnsSingleValue()
    {
        var response = BuildReadResponse(1, 0x04, [0xABCD]);

        var result = _parser.ParseReadRegisters(response);

        result.Should().HaveCount(1);
        result[0].Should().Be(0xABCD);
    }

    [Fact]
    public void ParseReadRegisters_FiveRegisters_ReturnsAllValues()
    {
        ushort[] values = [0x0001, 0x0002, 0x0003, 0x0004, 0x0005];
        var response = BuildReadResponse(1, 0x03, values);

        var result = _parser.ParseReadRegisters(response);

        result.Should().Equal(values);
    }

    // ── Error handling ───────────────────────────────────────────────────────

    [Fact]
    public void ParseReadRegisters_FrameTooShort_ThrowsInvalidDataException()
    {
        // Minimum is MbapLength(7) + 2 = 9 bytes
        byte[] response = new byte[8];

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<InvalidDataException>()
            .WithMessage("*too short*");
    }

    [Fact]
    public void ParseReadRegisters_ErrorResponse_ThrowsModbusProtocolException()
    {
        var response = BuildErrorResponse(255, 0x03, (byte)ModbusExceptionCode.IllegalDataAddress);

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.ReadHoldingRegisters)
            .Where(e => e.ExceptionCode == ModbusExceptionCode.IllegalDataAddress);
    }

    [Fact]
    public void ParseReadRegisters_ErrorResponse_FC04_ThrowsCorrectFunctionCode()
    {
        var response = BuildErrorResponse(1, 0x04, (byte)ModbusExceptionCode.IllegalFunction);

        var act = () => _parser.ParseReadRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.ReadInputRegisters);
    }

    // ── ValidateWrite ────────────────────────────────────────────────────────

    [Fact]
    public void ValidateWriteSingleRegister_ErrorResponse_Throws()
    {
        var response = BuildErrorResponse(1, 0x06, (byte)ModbusExceptionCode.IllegalDataValue);

        var act = () => _parser.ValidateWriteSingleRegister(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.WriteSingleRegister);
    }

    [Fact]
    public void ValidateWriteMultipleRegisters_ErrorResponse_Throws()
    {
        var response = BuildErrorResponse(1, 0x10, (byte)ModbusExceptionCode.ServerDeviceFailure);

        var act = () => _parser.ValidateWriteMultipleRegisters(response);

        act.Should().Throw<ModbusProtocolException>()
            .Where(e => e.FunctionCode == FunctionCode.WriteMultipleRegisters);
    }

    // ── ParseReportSlaveId ───────────────────────────────────────────────────

    [Fact]
    public void ParseReportSlaveId_ValidResponse_ReturnsParsedData()
    {
        // MBAP(7) + FC(1) + ByteCount(1) + [SlaveId, RunIndicator, extra...]
        byte[] payload = [0xF2, 0xFF, 0x01, 0x02];
        var frame = new byte[9 + payload.Length];
        BinaryPrimitives.WriteUInt16BigEndian(frame, 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(1 + 2 + payload.Length));
        frame[6] = 1;
        frame[7] = 0x11;
        frame[8] = (byte)payload.Length;
        Array.Copy(payload, 0, frame, 9, payload.Length);

        var result = _parser.ParseReportSlaveId(frame);

        result.RawData.Should().Equal(payload);
        result.RunIndicatorStatus.Should().Be(0xFF);
    }

    [Fact]
    public void ParseReportSlaveId_MinimalPayload_RunIndicatorDefaultsToZero()
    {
        byte[] payload = [0xF2];
        var frame = new byte[10];
        BinaryPrimitives.WriteUInt16BigEndian(frame, 0x0001);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(2), 0);
        BinaryPrimitives.WriteUInt16BigEndian(frame.AsSpan(4), (ushort)(1 + 2 + payload.Length));
        frame[6] = 1;
        frame[7] = 0x11;
        frame[8] = (byte)payload.Length;
        frame[9] = payload[0];

        var result = _parser.ParseReportSlaveId(frame);

        result.RawData.Should().Equal(payload);
        result.RunIndicatorStatus.Should().Be(0x00);
    }
}
