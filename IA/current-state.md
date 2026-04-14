  ---                                                                  
  Project Structure                                         
                                                                       
  ModbusApp/                                                
  ├── Modbus.Core/         ← All logic, NO UI                        
  └── Modbus.Desktop/      ← Avalonia UI, wires everything together    
                                                                       
  Think of it like a restaurant:                                       
  - Modbus.Core is the kitchen — it does all the real work             
  - Modbus.Desktop is the dining room — it presents information to the 
  user                                                               
                                                                       
  ---                                                       
  Modbus.Core — Layer by Layer                                         
                                                                       
  Layer 1: Domain (Domain/)                                          
                                                                       
  These are plain C# classes that represent your data. No logic, just  
  structure.                                                         
                                                                       
  ModbusDevice       — a physical device (has IP/COM port, slave ID,
  name)                                                                
  DeviceModel        — a device type/template (e.g. "PowerMeter X200")
  RegisterDefinition — one register in a device model (address, name,  
  unit, scale)                                                         
  RegisterValue      — a reading from a device (address + value +      
  timestamp)                                                           
                                                                     
  DeviceModel has many RegisterDefinitions.                            
  ModbusDevice belongs to a DeviceModel.                    
  ModbusDevice has many RegisterValues (historical readings).          
                                                                       
  Layer 2: Protocol (Protocol/)                                      
                                                                       
  Handles the raw Modbus wire format — building and parsing byte       
  arrays.                                                            
                                                                       
  ModbusTcpFrameBuilder   — builds TCP packets (adds MBAP header:
  transaction ID, length, etc.)                                        
  ModbusTcpFrameParser    — reads TCP responses back into register
  values                                                               
  ModbusRtuFrameBuilder   — builds RTU packets (adds slave ID + CRC16
  checksum at the end)                                                 
  ModbusRtuFrameParser    — reads RTU responses
                                                                       
  You never touch this layer directly. It's used internally by the     
  transport layer.                                                   
                                                                       
  Layer 3: Transport (Transport/)                           
                                                                     
  Handles the actual connection — opening sockets, sending bytes,      
  receiving bytes.
                                                                       
  IModbusTransport       — the interface (contract)         
  TcpModbusTransport     — opens a TCP socket to an IP:port,         
  sends/receives bytes                                                 
  RtuModbusTransport     — opens a serial port (COM/ttyS),
  sends/receives bytes                                                 
                                                            
  The transport doesn't know what the bytes mean. It just moves them.  
                                                            
  Layer 4: Services (Services/)                                        
                                                            
  This is the main entry point your app talks to. It combines transport
   + protocol.
                                                                       
  IModbusService         — the contract: ConnectAsync,      
  ReadHoldingRegisters, WriteSingle, etc.                              
  ModbusService          — implements IModbusService using a transport
  + frame builder/parser                                               
  IModbusServiceFactory  — creates an IModbusService for a given
  ModbusDevice                                                         
  ModbusServiceFactory   — looks at the device's TransportType and
  creates the right service                                            
                                                            
  One IModbusService = one connection to one device.                   
                                                            
  Layer 5: Persistence (Persistence/)                                  
                                                            
  Saves and loads data from a local SQLite database using EF Core.     
   
  ModbusDbContext                  — the EF Core "gateway" to the      
  database                                                             
  DeviceRepository                 — load/save/delete ModbusDevice   
  records                                                              
  DeviceModelRepository            — load/save DeviceModel records
  RegisterValueRepository          — load/upsert RegisterValue records 
   
  EF Core handles the SQL for you. You just call GetAllAsync() and get 
  back C# objects.                                          
                                                                       
  Layer 6: Polling (Polling/)                               
                                                                     
  Runs in the background, continuously reading all devices.

  IPollingEngine         — start/stop polling, add/remove devices
  PollingEngine          — every 5 seconds, for each active device:    
                             1. Connect if not connected
                             2. Read all registers defined in the      
  device model                                              
                             3. Fire RegisterValuesUpdated event (UI   
  updates)                                                             
                             or fire DeviceConnectionFailed event (on
  error)                                                               
                                                            
  The polling engine is the "heartbeat" of the app. It fires events    
  that the UI listens to.                                   
                                                                       
  ---                                                       
  Modbus.Desktop — Layer by Layer                                    
                                 
  Dependency Injection (App.axaml.cs)
                                                                       
  When the app starts, before any window opens, we register all        
  services into a container:                                           
                                                                       
  services.AddDbContext<ModbusDbContext>(...)   // database          
  services.AddTransient<IDeviceRepository>()   // repositories         
  services.AddSingleton<IPollingEngine>()       // one global polling  
  engine                                                               
  services.AddSingleton<MainViewModel>()        // one main VM for the 
  window                                                               
                                                                     
  Transient = a new instance every time you ask for one.               
  Singleton = one instance shared across the entire app.    
                                                                       
  The container is like a factory — instead of new                     
  DeviceRepository(db), you ask the container and it figures out the   
  dependencies for you.                                                
                                                                     
  ViewModels (ViewModels/)                                           

  ViewModels are the bridge between the data and the UI. They hold     
  state and commands. The UI binds to them.
                                                                       
  MainViewModel          — owns the sidebar navigation and the current
  visible page                                                         
  DeviceListViewModel    — holds the list of devices, triggers load,
  handles polling events                                               
  DeviceItemViewModel    — wraps one ModbusDevice, tracks   
  IsConnected/HasError/LastSeenAt                                      
  DeviceDetailViewModel  — holds register values for one device, loaded
   from DB                                                             
  RegisterValueViewModel — wraps one RegisterValue + its    
  RegisterDefinition (for the name/unit)                               
  NavItem                — just a title + icon for the sidebar button
                                                                       
  CommunityToolkit.Mvvm generates the boilerplate for you:             
  - [ObservableProperty] on _isConnected → auto-generates IsConnected  
  property + PropertyChanged event                                     
  - [RelayCommand] on LoadDevicesAsync() → auto-generates            
  LoadDevicesCommand you can bind buttons to                           
                                                                       
  Views (Views/)                                                     
                                                                       
  Views are the visual layer. They are AXAML files (similar to         
  XML/HTML) that describe layout and bind to ViewModel properties.   
                                                                       
  MainWindow.axaml       — the shell: 220px dark sidebar + content area
   on the right                                                        
  DeviceListView.axaml   — list of device cards
  DeviceDetailView.axaml — device info + register values table         
                                                            
  Key concept — DataTemplates:                                         
  In App.axaml, we define:                                  
  <DataTemplate DataType="{x:Type vm:DeviceListViewModel}">            
      <views:DeviceListView />                              
  </DataTemplate>                                                      
  This tells Avalonia: "when you see a DeviceListViewModel as content, 
  render it using DeviceListView".                                   
                                                                       
  The MainWindow has:                                       
  <ContentControl Content="{Binding CurrentPage}" />                   
  When CurrentPage changes (e.g. from DeviceListViewModel to
  DeviceDetailViewModel), Avalonia automatically swaps the view. This  
  is navigation without any navigator class.                           
                                                                       
  ---                                                                  
  Data Flow — From Hardware to Screen                                
                                                                       
  Here's what happens when a device is polled:
                                                                       
  PollingEngine (background thread)                         
      → TcpModbusTransport.SendAsync()       [opens socket, sends      
  bytes]                                                               
      → ModbusTcpFrameParser.Parse()         [decodes the response]    
      → fires RegisterValuesUpdated event                              
                                                                     
  DeviceListViewModel (subscriber)                                     
      → Dispatcher.UIThread.InvokeAsync()    [marshal to UI thread -
  IMPORTANT]                                                           
      → DeviceItemViewModel.IsConnected = true              
      → DeviceItemViewModel.LastSeenAt = now                           
                                                            
  UI (Avalonia binding)                                                
      → TextBlock.Text = "{Binding StatusText}"  [auto-updates because
  of INotifyPropertyChanged]                                           
                                                            
  The Dispatcher.UIThread.InvokeAsync step is critical — you can never 
  update UI properties from a background thread in any UI framework.
  The dispatcher queues the update onto the UI thread safely.          
                                                            
  ---                                                                
 * Add device implementation *
 All done. Here's a summary of everything that was built:            
                                                                      
  ---                                   
  What was implemented                                                
                                                                      
  Modbus.Core — 8 files                                               
                                                                      
  File: Domain/Entities/DeviceModel.cs                                
  Change: Added byte? DeviceCode property                             
  ────────────────────────────────────────                            
  File: Persistence/Configurations/DeviceModelConfiguration.cs        
  Change: Configured DeviceCode as nullable column                    
  ────────────────────────────────────────                          
  File: Services/DeviceCodeRegistry.cs                                
  Change: Static registry: 0xF2 → "KS-3000"                           
  ────────────────────────────────────────                            
  File: Services/Scanning/DeviceScanResult.cs                         
  Change: Scan result model with FirmwareVersionText/SerialNumberText
    helpers                                                           
  ────────────────────────────────────────                            
  File: Services/Scanning/ScanProgress.cs                             
  Change: Progress model (Current, Total, Found, CurrentLabel)        
  ────────────────────────────────────────                            
  File: Services/Scanning/IDeviceScanService.cs                       
  Change: Interface with ScanRtuAsync / ScanTcpAsync returning        
    IAsyncEnumerable<DeviceScanResult>                             
  ────────────────────────────────────────                            
  File: Services/Scanning/DeviceScanService.cs                        
  Change: RTU: one port, iterates addresses. TCP: one connection per  
    IP. Reads serial from Input Reg 0–1 (UInt32 BE). Decodes device   
    code via registry.                                                
  ────────────────────────────────────────                  
  File: Persistence/DeviceModelSeeder.cs                              
  Change: Idempotent startup seeder — inserts KS-3000 (and any future
    registry entries) if not already present                          
                                                            
  Modbus.Desktop — 8 files  

  File: ViewModels/ScanResultViewModel.cs                           
  Change: Immutable display wrapper around DeviceScanResult         
  ────────────────────────────────────────                          
  File: ViewModels/AddDeviceViewModel.cs                            
  Change: Full scan + form VM: transport toggle, RTU/TCP params, async
                                                                      
    cancellable scan, auto-fill on result selection, save flow with
    polling reload                                                    
  ────────────────────────────────────────                  
  File: Views/AddDeviceView.axaml                                     
  Change: Complete UI: transport radio, RTU/TCP param panels, scan
    button + progress, results list, device details form, save button 
  ────────────────────────────────────────                  
  File: Views/AddDeviceView.axaml.cs                                  
  Change: Code-behind
  ────────────────────────────────────────                            
  File: ViewModels/DeviceListViewModel.cs                             
  Change: Added IDeviceScanService + IDeviceModelRepository injection;
                                                                      
    OpenAddDeviceCommand; LoadDevicesAsync made internal    
  ────────────────────────────────────────
  File: Views/DeviceListView.axaml                                    
  Change: Added + Add Device button in header
  ────────────────────────────────────────                            
  File: App.axaml                                                     
  Change: Added DataTemplate for AddDeviceViewModel → AddDeviceView
  ────────────────────────────────────────                            
  File: App.axaml.cs                                                  
  Change: Registered IDeviceScanService; seeder runs at startup after
    EnsureCreated                                                     
                                                            
  To test with your KS-3000 on COM5, 9600 8N2                         
   
  1. Launch app → delete modbus.db first if it existed before (to pick
   up the new DeviceCode column)                            
  2. Click + Add Device → RTU is pre-selected                         
  3. Select COM5, baud 9600, parity None, stop bits Two, data bits 8
  4. Set address range 1 – 10 (or 1 – 1 for a quick test)             
  5. Click Search Network — the KS-3000 at address 1 should appear    
  with its serial number auto-filled                                  
  6. Click it → name is auto-filled as KS-3000 #XXXXXXXX              
  7. Click Save Device → device appears in the list and polling begins


Now the software is adding devices using de UDP search broadcast

UDP search command is now send to all network adapters

The software now has PT-BR localization, and PT-BR is the dafault language on new installations:
New files:

Services/LocalizationService.cs — singleton with string this[string key] indexer; raises PropertyChanged("Item[]") on switch; persists to %LOCALAPPDATA%\ModbusApp\lang.txt
Services/Strings/EnglishStrings.cs — EN dictionary (~58 keys)
Services/Strings/PortugueseStrings.cs — PT-BR dictionary (same keys)
Services/AppLanguageConverter.cs — displays "English" / "Português (BR)" in the ComboBox
ViewModels/SettingsViewModel.cs — exposes language selector bound to the service
Views/SettingsView.axaml + .cs — Settings page with language ComboBox
Modified files:

NavItem → now ObservableObject with stable Key + reactive Title
MainViewModel → adds Settings nav item, syncs nav titles on language change
DeviceItemViewModel → IDisposable, subscribes to language changes for StatusText/ModelDisplayName/SlaveIdText/LastSeenText
All 3 views (DeviceListView, AddDeviceView, DeviceDetailView) → fully localized bindings
All status-producing ViewModels → use LocalizationService.Instance["key"] and string.Format
App.axaml.cs / App.axaml → DI registration + DataTemplate for Settings


** Plan for adding the readings page and read somo input registers **

# Plan: Electrical Readings Page ("Leituras em Tempo Real")

## Context

The user wants to add an "Electrical Readings" page to the device detail view. When opening a device, instead of just showing a raw DataGrid of register values, the view should display a modern tabbed interface with the first tab showing **real-time metering data** (voltages, currents, power, power factor, frequency) in a card-based layout. Data comes from KS-3000 input registers (FC 04) and should update live via the polling engine.

The KS-3000 documentation defines ~30 input registers at addresses 0-65 (all IEEE 32-bit float except serial number which is UInt32). Currently, no register definitions are seeded for the KS-3000 model, so the polling engine only does a heartbeat read.

---

## Implementation Steps

### Step 1: Seed KS-3000 Register Definitions

**File:** `Modbus.Core/Persistence/DeviceModelSeeder.cs`

Extend `SeedAsync` to populate KS-3000 registers if none exist. `GetByNameAsync` already includes registers via `.Include()`, so we can check `existing.Registers.Count == 0`.

Add all ~30 input register definitions:
- Address 0: NS (UInt32, no unit)
- Addresses 2-14: Voltages U0, U12, U23, U31, U1, U2, U3 (Float32, V)
- Address 16: I0 (Float32, A), skip reserved 18-19
- Addresses 20-24: I1, I2, I3 (Float32, A)
- Address 26: Freq (Float32, Hz), skip reserved 28-33
- Addresses 34-40: P0-P3 (Float32, W)
- Addresses 42-48: Q0-Q3 (Float32, VAr)
- Addresses 50-56: S0-S3 (Float32, VA)
- Addresses 58-64: FP0-FP3 (Float32, no unit)

All with: `RegisterType = Input`, `WordOrder = LittleEndian` (KS-3000 uses F2,F1,F0,EXP = CDAB), `ScaleFactor = 1.0`, `IsWritable = false`.

### Step 2: Create `ElectricalReadingViewModel`

**New file:** `Modbus.Desktop/ViewModels/ElectricalReadingViewModel.cs`

Observable item for a single register reading:
- Properties: `Name` (register code e.g. "U0"), `Description` (localized), `DisplayValue` (formatted with unit), `Unit`, `Value` (double), `Address` (ushort)
- `Update(double newValue)` method that refreshes `Value` and `DisplayValue`

### Step 3: Create `ReadingGroupViewModel`

**New file:** `Modbus.Desktop/ViewModels/ReadingGroupViewModel.cs`

Groups readings by category:
- Properties: `GroupName` (localized), `Readings` (ObservableCollection of ElectricalReadingViewModel)
- 7 groups: Voltages, Currents, Frequency, Active Power, Reactive Power, Apparent Power, Power Factor

### Step 4: Modify `DeviceDetailViewModel`

**File:** `Modbus.Desktop/ViewModels/DeviceDetailViewModel.cs`

- Add `IPollingEngine` constructor parameter
- Subscribe to `RegisterValuesUpdated` event (filter by device ID)
- Add `ObservableCollection<ReadingGroupViewModel> ReadingGroups` property
- Add `int SelectedTabIndex` observable property
- Build reading groups from `Device.Device.DeviceModel.Registers`, mapping name prefixes to groups
- On polling event: marshal to UI thread, find matching `ElectricalReadingViewModel` by address, call `Update()`
- Implement `IDisposable` to unsubscribe from polling event
- Call `Dispose()` in `GoBack()`

### Step 5: Update `DeviceListViewModel`

**File:** `Modbus.Desktop/ViewModels/DeviceListViewModel.cs`

Pass `_pollingEngine` when creating DeviceDetailViewModel (line 91):
```csharp
var detail = new DeviceDetailViewModel(device, _registerValueRepository, _pollingEngine, this);
```

### Step 6: Redesign `DeviceDetailView.axaml`

**File:** `Modbus.Desktop/Views/DeviceDetailView.axaml`

Keep header + loading bar + device info card. Replace the register DataGrid section with a `TabControl`:

- **Tab 1: "Leituras em Tempo Real"** — ScrollViewer with ItemsControl bound to `ReadingGroups`. Each group rendered as a dark card (`#252540`, CornerRadius 8) with group name header and a `UniformGrid` (Columns=4) of reading items. Each reading item shows the description (small, dim) above the value (large font) with unit.

- **Tab 2: "Registradores"** — The existing raw DataGrid (moved here for reference/debug use)

### Step 7: Add Localization Keys

**Files:** `Modbus.Desktop/Services/Strings/EnglishStrings.cs`, `PortugueseStrings.cs`

Keys needed:
- Tab names: `TabRealTime`, `TabRawRegisters`
- Group names: `GroupVoltages`, `GroupCurrents`, `GroupFrequency`, `GroupActivePower`, `GroupReactivePower`, `GroupApparentPower`, `GroupPowerFactor`
- Empty state: `NoLiveData`
- Register descriptions: `RegU0`, `RegU12`, `RegU23`, `RegU31`, `RegU1`-`RegU3`, `RegI0`-`RegI3`, `RegFreq`, `RegP0`-`RegP3`, `RegQ0`-`RegQ3`, `RegS0`-`RegS3`, `RegFP0`-`RegFP3`

---

## Key Files

| File | Action |
|------|--------|
| `Modbus.Core/Persistence/DeviceModelSeeder.cs` | Modify - add register definitions |
| `Modbus.Desktop/ViewModels/ElectricalReadingViewModel.cs` | Create |
| `Modbus.Desktop/ViewModels/ReadingGroupViewModel.cs` | Create |
| `Modbus.Desktop/ViewModels/DeviceDetailViewModel.cs` | Modify - polling subscription, tabs, groups |
| `Modbus.Desktop/ViewModels/DeviceListViewModel.cs` | Modify - pass polling engine |
| `Modbus.Desktop/Views/DeviceDetailView.axaml` | Modify - tabbed layout with cards |
| `Modbus.Desktop/Services/Strings/EnglishStrings.cs` | Modify - add keys |
| `Modbus.Desktop/Services/Strings/PortugueseStrings.cs` | Modify - add keys |

## Reuse

- `RegisterDecoder.Decode()` — already handles Float32 + LittleEndian word order
- `PollingEngine.GroupRegisters()` — already groups contiguous registers efficiently (maxGap=5 handles the gaps in KS-3000 register map)
- `DeviceModelRepository.GetByNameAsync()` — already includes registers
- `Dispatcher.UIThread.InvokeAsync()` pattern — same as DeviceListViewModel for thread-safe UI updates

## Verification

1. **Delete** `modbusapp.db` (force re-seed with register definitions)
2. **Launch app** — KS-3000 model should now have ~30 register definitions
3. **Open a KS-3000 device** — should see the tabbed detail view
4. **Real-time tab** — should show 7 grouped cards with readings updating every polling cycle (5s)
5. **Raw Registers tab** — should show the existing DataGrid with all register values
6. **Switch language** — all labels, group names, and register descriptions should update
7. **Navigate back** — verify no memory leak (polling event unsubscribed)

## Note on Word Order

The KS-3000 doc says "F2,F1,F0,EXP" which is LittleEndian word order (low word at lower address). If real device testing shows wrong values, change `WordOrder` to `BigEndian` in the seeder — that's the only change needed.

All done. Build passes with 0 errors. Here's a summary of what was implemented:

Modbus.Core (3 files)

DeviceModelSeeder.cs — Seeds 29 KS-3000 input register definitions (voltages, currents, frequency, active/reactive/apparent power, power factor) with Float32 + LittleEndian word order
IDeviceModelRepository.cs — Added UpdateAsync method
DeviceModelRepository.cs — Implemented UpdateAsync
Modbus.Desktop (7 files)

ElectricalReadingViewModel.cs — New. Observable item for a single live reading with Update() method
ReadingGroupViewModel.cs — New. Groups readings by category (localized group name)
DeviceDetailViewModel.cs — Subscribes to polling engine events for live updates, builds 7 reading groups from register definitions, implements IDisposable
DeviceListViewModel.cs — Passes _pollingEngine to DeviceDetailViewModel
DeviceDetailView.axaml — Tabbed layout: "Leituras em Tempo Real" tab with card-based grouped readings + "Registradores Brutos" tab with existing DataGrid
EnglishStrings.cs — 40+ new localization keys
PortugueseStrings.cs — Matching PT-BR keys



Whats next?
  - Create a page to show the energy readings
  - Do some readings on input registers like Real-time readings, energy, demands...
  - POC of the mobile version using the same core from the desktop version

  
  