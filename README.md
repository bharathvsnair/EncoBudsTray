# EncoBudsTray

Minimal Windows system-tray battery monitor for **OPPO Enco Buds2**.

EncoBudsTray does one thing: show the battery status of your earbuds and charging case from the Windows system tray.

![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![.NET](https://img.shields.io/badge/.NET-8-512BD4)
![License](https://img.shields.io/badge/license-Apache--2.0-green)
License: Apache-2.0

## ✨ Features

- Left earbud battery
- Right earbud battery
- Charging-case battery
- Automatic Bluetooth Classic RFCOMM connection
- Automatic reconnect after communication failure
- Periodic battery updates
- Manual **Refresh**
- Simple tray tooltip
- Lightweight local logging
- No background Bluetooth scanning while connected

The tray menu intentionally contains only **Refresh** and **Exit**.

---

## 📦 Installation

### Recommended: Standalone executable

Download the Windows release and run:

```text
EncoBudsTray.exe
```

The standalone build includes the required .NET runtime, so **you do not need to install .NET separately**.

Pair your **OPPO Enco Buds2** with Windows first and make sure Bluetooth is enabled.

> **Note:** The current release targets `win-x64`.

---

## 🚀 Usage

1. Pair the OPPO Enco Buds2 with Windows.
2. Start `EncoBudsTray.exe`.
3. The application appears in the Windows system tray.
4. Hover over the tray icon to see the current battery levels.
5. Use **Refresh** from the tray menu to request an immediate update.

Example:

```text
OPPO Enco Buds2
Left: 70%
Right: 90%
Case: 40%
```

Unavailable values are shown as:

```text
—
```

If the earbuds are disconnected:

```text
OPPO Enco Buds2 — Disconnected
```

---

## Requirements

- Windows 10 version 2004 (build 19041) or later
- OPPO Enco Buds2 paired/available through Windows Bluetooth
- Bluetooth enabled

The **.NET 8 SDK** is only required when building from source.

---

## 🛠️ Building from Source

From the project directory:

```powershell
dotnet restore
dotnet build -c Release
```

Run locally:

```powershell
dotnet run -c Release
```

### Run tests

```powershell
dotnet test .\Tests\EncoBudsTray.Tests.csproj
```

Bluetooth hardware is not required for the protocol tests.

### Publish a standalone executable

```powershell
dotnet publish .\EncoBudsTray.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true
```

The published executable is placed under:

```text
bin\Release\net8.0-windows10.0.19041.0\win-x64\publish\
```

---

## 🔬 How It Works

EncoBudsTray uses the OPPO Enco Buds2 communication path documented by the supplied **Gadgetbridge** source.

Gadgetbridge identifies the device using the exact name:

```text
OPPO Enco Buds2
```

The supported connection uses **Bluetooth Classic RFCOMM** with this service UUID:

```text
0000079a-d102-11e1-9b23-00025b00a5a5
```

EncoBudsTray discovers that RFCOMM service and verifies the device name before connecting.

It does not require a manually entered Bluetooth address.

Once connected, the application:

1. Sends the battery request.
2. Reads the response.
3. Parses the battery values.
4. Updates the tray tooltip.
5. Repeats the process approximately every 30 seconds.

---

## 📡 Battery Protocol

Only the battery request/response portion of the OPPO protocol is implemented.

### Request

Gadgetbridge defines:

```text
BATTERY_REQ = 0x0106
```

The request uses Gadgetbridge's common message framing.

For an empty battery-request payload, the initial request is:

```text
AA 07 00 00 06 01 00 00 00
```

The sequence byte increments for subsequent requests.

### Response

The battery response command is:

```text
BATTERY_RET = 0x8106
```

Battery entries use these mappings:

```text
protocol index 1 → Left
protocol index 2 → Right
protocol index 3 → Case
```

The battery level is obtained with:

```text
raw byte & 0x7F
```

The high bit:

```text
raw byte & 0x80
```

represents the charging state.

Gadgetbridge ignores a case reading when the case level is zero, and EncoBudsTray preserves that behavior.

Missing or invalid values are represented internally as `null` rather than `0%`.

---

## 🔄 Connection & Update Behavior

The application:

1. Searches for the Enco Buds2 RFCOMM service.
2. Connects using the Windows RFCOMM API.
3. Sends only the battery request.
4. Reads and parses the response.
5. Updates the tray tooltip.
6. Polls again approximately every 30 seconds.
7. Retries battery requests when necessary.
8. Automatically reconnects after communication failure.

Initial discovery uses a short reconnect delay with exponential backoff up to 30 seconds.

A manual **Refresh** wakes the monitor immediately.

The application does not continuously scan for Bluetooth devices while a connection is active.

---

## 🧪 Tests

The test suite covers:

- Gadgetbridge battery-response parsing
- Left/right/case mapping
- Charging-bit parsing
- Gadgetbridge's zero-case behavior
- Battery request construction
- Multiple and partial response framing
- Malformed payload lengths

The current test suite passes all **5 tests**.

Bluetooth hardware is **not required** for these tests.

---

## 📝 Logging

Lightweight logs are written locally to:

```text
%LOCALAPPDATA%\EncoBudsTray\enco.log
```

By default, only the following are logged:

- Connection state
- Errors
- Parsed battery values

Raw protocol packets are **not logged by default**.

---

## 🔍 Gadgetbridge Source Traceability

The supplied Gadgetbridge archive was the **sole technical protocol/device source** used to implement the OPPO communication path.

Relevant Gadgetbridge source locations include:

### `OppoEncoBuds2Coordinator.java`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/devices/oppo/OppoEncoBuds2Coordinator.java
```

Used for:

- Exact supported device name: `OPPO Enco Buds2`
- Device-specific coordinator identity

### `OppoHeadphonesCoordinator.java`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/devices/oppo/OppoHeadphonesCoordinator.java
```

Used for:

- Bluetooth Classic coordinator type
- Three battery slots
- Battery index semantics:
  - `0` = left
  - `1` = right
  - `2` = case

### `OppoHeadphonesSupport.java`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesSupport.java
```

Used for:

- RFCOMM service UUID
- OPPO serial/RFCOMM support path
- Battery request behavior
- Battery retry behavior

### `OppoHeadphonesProtocol.java`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesProtocol.java
```

Used for:

- OPPO frame structure
- Battery request encoding
- Battery response decoding
- Battery index mapping
- Charging-bit interpretation
- Unknown-value behavior

### `OppoCommand.java`

```text
app/src/main/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/commands/OppoCommand.java
```

Used for:

- `BATTERY_REQ = 0x0106`
- `BATTERY_RET = 0x8106`

### Protocol test reference

```text
app/src/test/java/nodomain/freeyourgadget/gadgetbridge/service/devices/oppo/OppoHeadphonesProtocolTest.java
```

The supplied Gadgetbridge battery-response sample was used as a parser test reference.

EncoBudsTray does **not** copy Gadgetbridge implementation code.

---

## 🙏 Acknowledgements

### Gadgetbridge

This project would not have been possible without the protocol and device research contained in **Gadgetbridge**.

Gadgetbridge is licensed under the **GNU Affero General Public License v3 (AGPLv3)**. Its source was used as the technical reference for the OPPO Enco Buds2 communication behavior implemented here.

EncoBudsTray is an independent C# implementation and does not include copied Gadgetbridge source files.

### obudsmanager

The existence of the community **obudsmanager** project was an inspiration for building a small, focused Windows utility for OPPO earbuds.

No protocol implementation or source code from obudsmanager was used as the technical basis of EncoBudsTray.

---

## 🎯 Scope

EncoBudsTray deliberately has a narrow scope.

It does **not** implement:

- EQ
- ANC or listening modes
- Game mode
- Touch controls
- Firmware operations
- Audio or media controls
- Earbud configuration
- Account/cloud services
- A large settings interface

The goal is a small, focused utility that answers one question:

> **How much battery do my Enco Buds2 have?**

---

## ⚠️ Limitations

- The application requires Windows to expose the Gadgetbridge-supported RFCOMM service through the Windows Bluetooth APIs.
- The current release targets `win-x64`.
- Bluetooth behavior can vary between Windows Bluetooth adapters and drivers.
- Battery updates depend on the earbuds responding to the supported protocol.
- This project is specifically designed for **OPPO Enco Buds2** and does not claim compatibility with other OPPO or OnePlus earbuds.

---

## 🔒 Privacy

EncoBudsTray does not require an account or cloud service.

It communicates locally with the paired earbuds over Bluetooth and writes only its lightweight application log to:

```text
%LOCALAPPDATA%\EncoBudsTray
```

---

## 📜 License

EncoBudsTray is licensed under the **Apache License 2.0**.

See [`LICENSE`](LICENSE) for the full license text.

The Apache-2.0 license applies to the original EncoBudsTray code. Gadgetbridge remains licensed under its own AGPLv3 license; this project does **not** relicense Gadgetbridge.

---

**Made for a very specific problem: seeing your Enco Buds2 battery without opening a giant companion app.**