using System.Buffers.Binary;
using FluentAssertions;
using Modbus.Core.Protocol.Enums;
using Modbus.Core.Protocol.Tcp;

namespace Modbus.Core.Tests.Protocol.Tcp;

public class ModbusTcpFrameBuilderTests
{
    private readonly ModbusTcpFrameBuilder _builder = new();

    // ── ReadRegisters ────────────────────────────────────────────────────────

    [Fact]
    public void ReadRegisters_FC03_Builds12ByteFrame()
    {
        var frame = _builder.ReadRegisters(255, FunctionCode.ReadHoldingRegisters, 0, 10);

        frame.Should().HaveCount(12);
    }

    [Fact]
    public void ReadRegisters_FC03_HasCorrectMbapHeader()
    {
        var frame = _builder.ReadRegisters(255, FunctionCode.ReadHoldingRegisters, 0, 10);

        // Protocol ID = 0x0000
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(2)).Should().Be(0, "protocol ID");
        // Length = 6 (UnitId + 5-byte PDU)
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(6, "length field");
        // Unit ID
        frame[6].Should().Be(255, "unit ID");
    }

    [Fact]
    public void ReadRegisters_FC03_HasCorrectPdu()
    {
        var frame = _builder.ReadRegisters(255, FunctionCode.ReadHoldingRegisters, 100, 5);

        frame[7].Should().Be(0x03, "function code");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8)).Should().Be(100, "start address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(10)).Should().Be(5, "quantity");
    }

    [Fact]
    public void ReadRegisters_FC04_HasCorrectFunctionCode()
    {
        var frame = _builder.ReadRegisters(1, FunctionCode.ReadInputRegisters, 0, 1);

        frame[7].Should().Be(0x04);
    }

    [Fact]
    public void ReadRegisters_TransactionIdIncrements()
    {
        var frame1 = _builder.ReadRegisters(1, FunctionCode.ReadHoldingRegisters, 0, 1);
        var frame2 = _builder.ReadRegisters(1, FunctionCode.ReadHoldingRegisters, 0, 1);

        var txId1 = BinaryPrimitives.ReadUInt16BigEndian(frame1.AsSpan(0));
        var txId2 = BinaryPrimitives.ReadUInt16BigEndian(frame2.AsSpan(0));

        txId2.Should().Be((ushort)(txId1 + 1));
    }

    // ── WriteSingleRegister ──────────────────────────────────────────────────

    [Fact]
    public void WriteSingleRegister_Builds12ByteFrame()
    {
        var frame = _builder.WriteSingleRegister(1, 100, 0x1234);

        frame.Should().HaveCount(12);
        frame[7].Should().Be(0x06, "function code FC06");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8)).Should().Be(100, "address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(10)).Should().Be(0x1234, "value");
    }

    // ── WriteMultipleRegisters ───────────────────────────────────────────────

    [Fact]
    public void WriteMultipleRegisters_TwoValues_BuildsCorrectFrame()
    {
        var frame = _builder.WriteMultipleRegisters(1, 0, [0x000A, 0x0102]);

        // MBAP(7) + FC(1) + Addr(2) + Qty(2) + ByteCount(1) + Data(4) = 17
        frame.Should().HaveCount(17);
        frame[7].Should().Be(0x10, "function code FC16");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(8)).Should().Be(0, "start address");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(10)).Should().Be(2, "quantity");
        frame[12].Should().Be(4, "byte count");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(13)).Should().Be(0x000A, "first value");
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(15)).Should().Be(0x0102, "second value");
    }

    [Fact]
    public void WriteMultipleRegisters_LengthFieldIncludesData()
    {
        var frame = _builder.WriteMultipleRegisters(1, 0, [0x0001, 0x0002, 0x0003]);

        // Length = UnitId(1) + PDU: FC(1) + Addr(2) + Qty(2) + ByteCount(1) + Data(6) = 13
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(13, "length field");
    }

    // ── ReportSlaveId ────────────────────────────────────────────────────────

    [Fact]
    public void ReportSlaveId_Builds8ByteFrame()
    {
        var frame = _builder.ReportSlaveId(255);

        frame.Should().HaveCount(8);
        // Length = UnitId(1) + FC(1) = 2
        BinaryPrimitives.ReadUInt16BigEndian(frame.AsSpan(4)).Should().Be(2, "length field");
        frame[6].Should().Be(255, "unit ID");
        frame[7].Should().Be(0x11, "function code FC17");
    }
}
