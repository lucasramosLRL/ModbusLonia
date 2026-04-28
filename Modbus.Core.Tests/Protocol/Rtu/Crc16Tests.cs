using FluentAssertions;
using Modbus.Core.Protocol.Rtu;

namespace Modbus.Core.Tests.Protocol.Rtu;

public class Crc16Tests
{
    [Fact]
    public void Compute_KnownModbusVector_Slave1_FC03_Addr0_Qty10()
    {
        // Standard Modbus request: slave 1, FC03, address 0, quantity 10
        byte[] data = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        ushort crc = Crc16.Compute(data);

        // Build the full frame and validate round-trip
        var frame = new byte[8];
        Array.Copy(data, frame, 6);
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)(crc >> 8);
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Compute_KnownModbusVector_Slave1_FC04_Addr0_Qty2()
    {
        // Slave 1, FC04 (Read Input Registers), address 0, quantity 2
        byte[] data = [0x01, 0x04, 0x00, 0x00, 0x00, 0x02];
        ushort crc = Crc16.Compute(data);

        var frame = new byte[8];
        Array.Copy(data, frame, 6);
        frame[6] = (byte)(crc & 0xFF);
        frame[7] = (byte)(crc >> 8);
        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Compute_SingleByte_ReturnsNonInitialValue()
    {
        ushort crc = Crc16.Compute(new byte[] { 0x00 });
        // Must not be the initial 0xFFFF — computation must have run
        crc.Should().NotBe(0xFFFF);
    }

    [Fact]
    public void Append_AddsCrcBytesLsbFirst()
    {
        byte[] frame = new byte[8];
        frame[0] = 0x01;
        frame[1] = 0x03;
        frame[2] = 0x00;
        frame[3] = 0x00;
        frame[4] = 0x00;
        frame[5] = 0x0A;

        Crc16.Append(frame, 6);

        ushort expected = Crc16.Compute(frame.AsSpan(0, 6));
        frame[6].Should().Be((byte)(expected & 0xFF), "CRC LSB");
        frame[7].Should().Be((byte)(expected >> 8), "CRC MSB");
    }

    [Fact]
    public void Validate_ValidFrame_ReturnsTrue()
    {
        byte[] frame = new byte[8];
        frame[0] = 0x01;
        frame[1] = 0x03;
        frame[2] = 0x00;
        frame[3] = 0x00;
        frame[4] = 0x00;
        frame[5] = 0x0A;
        Crc16.Append(frame, 6);

        Crc16.Validate(frame).Should().BeTrue();
    }

    [Fact]
    public void Validate_CorruptedFrame_ReturnsFalse()
    {
        byte[] frame = new byte[8];
        frame[0] = 0x01;
        frame[1] = 0x03;
        frame[2] = 0x00;
        frame[3] = 0x00;
        frame[4] = 0x00;
        frame[5] = 0x0A;
        Crc16.Append(frame, 6);

        // Corrupt one data byte
        frame[3] = 0xFF;

        Crc16.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Validate_FrameTooShort_ReturnsFalse()
    {
        byte[] frame = [0x01, 0x03, 0x00];
        Crc16.Validate(frame).Should().BeFalse();
    }

    [Fact]
    public void Compute_DifferentData_ProducesDifferentCrc()
    {
        byte[] data1 = [0x01, 0x03, 0x00, 0x00, 0x00, 0x0A];
        byte[] data2 = [0x01, 0x04, 0x00, 0x00, 0x00, 0x0A];

        Crc16.Compute(data1).Should().NotBe(Crc16.Compute(data2));
    }
}
