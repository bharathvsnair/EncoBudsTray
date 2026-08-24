using EncoBudsTray.Models;
using EncoBudsTray.Protocol;

namespace EncoBudsTray.Tests;

public sealed class OppoBatteryProtocolTests
{
    [Fact]
    public void ParsesGadgetbridgeBatteryResponse()
    {
        var protocol = new OppoBatteryProtocol();

        var data = Convert.FromHexString(
            "AA0F000006810208000003016402640346");

        var result = protocol.ParseResponses(data);

        Assert.NotNull(result.Battery);
        Assert.Equal(100, result.Battery!.Left);
        Assert.Equal(100, result.Battery.Right);
        Assert.Equal(70, result.Battery.Case);
        Assert.False(result.Battery.LeftCharging);
        Assert.False(result.Battery.RightCharging);
        Assert.False(result.Battery.CaseCharging);
        Assert.Empty(result.Remaining);
    }

    [Fact]
    public void CreatesGadgetbridgeCompatibleBatteryRequest()
    {
        var protocol = new OppoBatteryProtocol();

        var request = protocol.CreateBatteryRequest();

        Assert.Equal(
            "AA090000060100000000",
            Convert.ToHexString(request));
    }

    [Fact]
    public void ParsesChargingBitAndIgnoresZeroCase()
    {
        var protocol = new OppoBatteryProtocol();

        // payload:
        // 00 status, 03 count,
        // 01 -> left, 0x9A -> 26% + charging,
        // 02 -> right, 0x05 -> 5%,
        // 03 -> case, 0x80 -> 0% + charging, ignored by Gadgetbridge.
        var frame = BuildBatteryFrame(
            0x01, 0x9A,
            0x02, 0x05,
            0x03, 0x80);

        var result = protocol.ParseResponses(frame);

        Assert.NotNull(result.Battery);
        Assert.Equal(26, result.Battery!.Left);
        Assert.Equal(5, result.Battery.Right);
        Assert.Null(result.Battery.Case);
        Assert.True(result.Battery.LeftCharging);
        Assert.False(result.Battery.RightCharging);
    }

    [Fact]
    public void ParsesMultipleResponsesAndLeavesPartialFrame()
    {
        var protocol = new OppoBatteryProtocol();

        var first = Convert.FromHexString("AA0F000006810208000003016402640346");
        var partial = new byte[] { 0xAA, 0x0F };

        var result = protocol.ParseResponses(first.Concat(partial).ToArray());

        Assert.NotNull(result.Battery);
        Assert.Equal(100, result.Battery!.Left);
        Assert.Equal(100, result.Battery.Right);
        Assert.Equal(70, result.Battery.Case);
        Assert.Equal("AA0F", Convert.ToHexString(result.Remaining));
    }

    [Fact]
    public void RejectsMalformedPayloadLength()
    {
        var protocol = new OppoBatteryProtocol();

        var malformed = Convert.FromHexString(
            "AA0F00000681020A000003016402640346");

        var result = protocol.ParseResponses(malformed);

        Assert.Null(result.Battery);
    }

    private static byte[] BuildBatteryFrame(params byte[] entries)
    {
        var payload = new byte[2 + entries.Length];
        payload[0] = 0;
        payload[1] = 3;
        entries.CopyTo(payload, 2);

        var frame = new byte[9 + payload.Length];
        frame[0] = 0xAA;
        frame[1] = (byte)(frame.Length - 2);
        frame[4] = 0x06;
        frame[5] = 0x81;
        frame[6] = 0;
        frame[7] = (byte)payload.Length;
        frame[8] = 0;
        payload.CopyTo(frame, 9);
        return frame;
    }
}
