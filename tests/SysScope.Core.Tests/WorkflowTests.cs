using System.Text;
using System.Text.Json;
using SysScope.Core.Collection;
using SysScope.Core.Demo;
using SysScope.Core.Export;
using SysScope.Core.Formatting;
using SysScope.Core.Models;
using SysScope.Core.Registry;
using SysScope.Core.Wmi;
using SysScope.Core.WinRm;

namespace SysScope.Core.Tests;

public class FormatterAndExportTests
{
    [Theory]
    [InlineData(0L, "0 B")]
    [InlineData(1536L, "1.5 KB")]
    public void Bytes_Format(long bytes, string expected)
    {
        Assert.Equal(expected, ByteSizeFormatter.Format(bytes));
    }

    [Fact]
    public void Bytes_FormatGigabytesAndNull()
    {
        Assert.Equal("1.0 GB", ByteSizeFormatter.Format(1024L * 1024 * 1024));
        Assert.Equal("", ByteSizeFormatter.Format(null));
        Assert.Equal("1.0 TB", ByteSizeFormatter.Format(1024L * 1024 * 1024 * 1024));
    }

    [Fact]
    public void Uptime_FormatsDaysHoursAndMinutes()
    {
        Assert.Equal("1d 2h 3m", UptimeFormatter.Format(new TimeSpan(1, 2, 3, 0)));
        Assert.Equal("4h 05m", UptimeFormatter.Format(TimeSpan.FromMinutes(245)));
        Assert.Equal("45m", UptimeFormatter.Format(TimeSpan.FromMinutes(45)));
        Assert.Equal("0m", UptimeFormatter.Format(TimeSpan.FromMinutes(-5)));
        Assert.Equal("", UptimeFormatter.Format(null));
    }

    [Fact]
    public void Mac_NormalizesTwelveHexDigits()
    {
        Assert.Equal("A4:83:E7:2C:10:04", MacAddressFormatter.Format("a4-83-e7-2c-10-04"));
        Assert.Equal("", MacAddressFormatter.Format("  "));
        Assert.Equal("SHORT", MacAddressFormatter.Format("short"));
    }

    [Fact]
    public void LinkSpeed_UsesDecimalUnits()
    {
        Assert.Equal("1 Gbps", LinkSpeedFormatter.Format(1_000_000_000));
        Assert.Equal("100 Mbps", LinkSpeedFormatter.Format(100_000_000));
        Assert.Equal("", LinkSpeedFormatter.Format(0));
    }

    [Fact]
    public void LoggedOnUser_BlankMeansNone()
    {
        Assert.Equal("None signed in", DisplayFormatter.LoggedOnUser(""));
        Assert.Equal(@"NORTHWIND\a.chen", DisplayFormatter.LoggedOnUser(@" NORTHWIND\a.chen "));
    }

    [Fact]
    public void Html_EscapesAndIncludesSections()
    {
        var report = new MachineReport
        {
            Online = true,
            CollectedAt = new DateTimeOffset(2026, 10, 19, 3, 0, 0, TimeSpan.Zero),
            Overview =
            {
                ComputerName = "A<B>&",
                Model = "Latitude",
                SecureBoot = SecureBootState.Enabled
            }
        };
        report.Overview.BitLocker.Add(new BitLockerVolume { DriveLetter = "C:", Status = "On", EncryptionMethod = "XTS-AES-128" });
        var html = HtmlReportWriter.Write(report);
        Assert.Contains("A&lt;B&gt;&amp;", html, StringComparison.Ordinal);
        Assert.Contains("#0B1F3A", html, StringComparison.Ordinal);
        Assert.Contains("#0D9488", html, StringComparison.Ordinal);
        Assert.Contains("BitLocker", html, StringComparison.Ordinal);
        Assert.Contains("XTS-AES-128", html, StringComparison.Ordinal);
        Assert.Contains(ProductInfo.Version, html, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Html_ShowsConnectionError()
    {
        var html = HtmlReportWriter.Write(new MachineReport { Online = false, Error = "Could not inventory FS-01.\nAccess was denied." });
        Assert.Contains("Could not inventory FS-01.", html, StringComparison.Ordinal);
        Assert.Contains("class=\"error\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void Json_RoundTripsKeyFields()
    {
        var report = new MachineReport
        {
            Online = true,
            Overview = { ComputerName = "NW-LAP-014", SerialNumber = "7F3K2C1", SecureBoot = SecureBootState.Enabled, OsBuild = "22631.4317" }
        };
        report.Hardware.Disks.Add(new PhysicalDisk { Model = "Samsung", Health = "Healthy" });
        var json = JsonReportWriter.Write(report);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("NW-LAP-014", doc.RootElement.GetProperty("overview").GetProperty("computerName").GetString());
        Assert.Equal("7F3K2C1", doc.RootElement.GetProperty("overview").GetProperty("serialNumber").GetString());
        Assert.Equal("Enabled", doc.RootElement.GetProperty("overview").GetProperty("secureBoot").GetString());
        Assert.Equal("Samsung", doc.RootElement.GetProperty("hardware").GetProperty("disks")[0].GetProperty("model").GetString());
    }

    [Fact]
    public void Csv_WritesBomHeadersAndEscapes()
    {
        var bytes = CsvInventoryWriter.Write(
        [
            new InventoryRow { Name = "NW,1", Online = "Yes", Model = "OptiPlex", Serial = "A\"B", Os = "Windows", Build = "1", Cpu = "CPU", Ram = "16.0 GB", DiskFree = "10.0 GB", LastBoot = "2026-10-01 08:00", LoggedOnUser = "user", Detail = "line\r\n2" },
            new InventoryRow { Name = "=HYPERLINK(\"http://x\")", Online = "No", Detail = "blocked" }
        ]);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
        var text = Encoding.UTF8.GetString(bytes);
        Assert.StartsWith("\uFEFFName,Online,Model,Serial,OS,Build,CPU,RAM,Disk free,Last boot,Logged-on user,Detail", text, StringComparison.Ordinal);
        Assert.Contains("\"NW,1\"", text, StringComparison.Ordinal);
        Assert.Contains("\"A\"\"B\"", text, StringComparison.Ordinal);
        Assert.Contains("\"'=HYPERLINK", text, StringComparison.Ordinal);
        Assert.Contains("\r\n", text, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductVersion_IsOnePointZero()
    {
        Assert.Equal("1.0.0", ProductInfo.Version);
        Assert.Equal("SysScope", ProductInfo.Name);
    }
}

public class ConnectionAndListTests
{
    [Theory]
    [InlineData("", true)]
    [InlineData(".", true)]
    [InlineData("localhost", true)]
    [InlineData(@"\\NW-DESK-102", false)]
    public void ConnectionTarget_LocalAndSlashes(string computer, bool local)
    {
        var target = new ConnectionTarget(computer, null, null);
        Assert.Equal(local, target.IsLocal);
        if (computer.Contains('\\'))
            Assert.Equal("NW-DESK-102", target.ComputerName);
    }

    [Fact]
    public void Credentials_BlankUserIsIgnored()
    {
        Assert.False(new ConnectionTarget("PC", "  ", "secret").HasCredentials);
        Assert.True(new ConnectionTarget("PC", @"NORTHWIND\a.chen", null).HasCredentials);
    }

    [Theory]
    [InlineData("The RPC server is unavailable. (0x800706BA)", TransportErrorCode.RpcServerUnavailable)]
    [InlineData("Access is denied. (0x80070005)", TransportErrorCode.AccessDenied)]
    [InlineData("WMI 0x80041003 access", TransportErrorCode.AccessDenied)]
    [InlineData("The operation timed out", TransportErrorCode.TimedOut)]
    [InlineData("No such host is known", TransportErrorCode.HostUnreachable)]
    [InlineData("WinRM cannot connect to the destination", TransportErrorCode.WinRmConnection)]
    public void Mapper_ClassifiesMessages(string message, TransportErrorCode code)
    {
        Assert.Equal(code, TransportExceptionMapper.Classify(message));
    }

    [Fact]
    public void Formatter_AccessDeniedIsActionable()
    {
        var text = ConnectionErrorFormatter.Format(new TransportException("DCOM", TransportErrorCode.AccessDenied, "Access is denied. (0x80070005)"));
        Assert.StartsWith("DCOM:", text, StringComparison.Ordinal);
        Assert.Contains("Access was denied", text, StringComparison.Ordinal);
        Assert.Contains("Remote Enable", text, StringComparison.Ordinal);
        Assert.Contains("0x80070005", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fallback_UsesDcomBeforeWinRm()
    {
        var session = new ScriptSession();
        var dcom = new Probe("DCOM", session: session);
        var winrm = new Probe("WinRM", session: new ScriptSession());
        var connector = new FallbackConnector([dcom, winrm]);
        var opened = await connector.ConnectAsync(new ConnectionTarget("PC", null, null), CancellationToken.None);
        Assert.Same(session, opened);
        Assert.Equal(1, dcom.Calls);
        Assert.Equal(0, winrm.Calls);
    }

    [Fact]
    public async Task Fallback_TriesWinRmAfterDcomFailure()
    {
        var session = new ScriptSession { Computer = "FS-01" };
        var dcom = new Probe("DCOM", new TransportException("DCOM", TransportErrorCode.RpcServerUnavailable, "The RPC server is unavailable."));
        var winrm = new Probe("WinRM", session: session);
        var connector = new FallbackConnector([dcom, winrm]);
        var opened = await connector.ConnectAsync(new ConnectionTarget("FS-01", null, null), CancellationToken.None);
        Assert.Same(session, opened);
        Assert.Equal(1, winrm.Calls);
    }

    [Fact]
    public async Task Fallback_BothFailWithClearMessage()
    {
        var connector = new FallbackConnector(
        [
            new Probe("DCOM", new TransportException("DCOM", TransportErrorCode.AccessDenied, "Access is denied.")),
            new Probe("WinRM", new TransportException("WinRM", TransportErrorCode.WinRmConnection, "The client cannot connect."))
        ]);
        var ex = await Assert.ThrowsAsync<CollectionFailedException>(() =>
            connector.ConnectAsync(new ConnectionTarget("FS-01", null, null), CancellationToken.None));
        Assert.Contains("FS-01", ex.Message, StringComparison.Ordinal);
        Assert.Contains("DCOM:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("WinRM:", ex.Message, StringComparison.Ordinal);
        Assert.Contains("Access was denied", ex.Message, StringComparison.Ordinal);
        Assert.Contains("5985", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fallback_LocalFailureNamesThisPc()
    {
        var connector = new FallbackConnector([new Probe("DCOM", new InvalidOperationException("boom"))]);
        var ex = await Assert.ThrowsAsync<CollectionFailedException>(() =>
            connector.ConnectAsync(new ConnectionTarget("", null, null), CancellationToken.None));
        Assert.Contains("this PC", ex.Message, StringComparison.Ordinal);
        Assert.Contains("boom", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Fallback_CancellationDoesNotTryNext()
    {
        var winrm = new Probe("WinRM", session: new ScriptSession());
        var connector = new FallbackConnector(
        [
            new Probe("DCOM", new OperationCanceledException()),
            winrm
        ]);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => connector.ConnectAsync(Support.Request("PC").Target, cts.Token));
        Assert.Equal(0, winrm.Calls);
    }

    [Fact]
    public void ComputerList_LinesCommentsAndDedupe()
    {
        var names = ComputerListParser.Parse("NW-DESK-102\n# skip me\n\nOFFLINE-PC\nnw-desk-102\nRECEPTION-01\n");
        Assert.Equal(["NW-DESK-102", "OFFLINE-PC", "RECEPTION-01"], names);
    }

    [Fact]
    public void ComputerList_CsvHeaderAndFirstColumn()
    {
        var headed = ComputerListParser.Parse("Computer Name,Site\nNW-DESK-102,Lab\nFS-LAB-007,Bench\n");
        Assert.Equal(["NW-DESK-102", "FS-LAB-007"], headed);
        var plain = ComputerListParser.Parse("alpha,extra\nbeta,extra\n");
        Assert.Equal(["alpha", "beta"], plain);
        Assert.Empty(ComputerListParser.Parse("   \n# only\n"));
    }

    [Fact]
    public void ComputerList_QuotedCommas()
    {
        var fields = ComputerListParser.SplitCsvLine("\"NW, Desk\",Lab");
        Assert.Equal("NW, Desk", fields[0]);
        var names = ComputerListParser.Parse("Name\n\"NW, Desk\"\n");
        Assert.Equal(["NW, Desk"], names);
    }
}

public class InventoryAndRegistryTests
{
    [Fact]
    public void InventoryRow_MapsOnlineAndOffline()
    {
        var online = InventoryRowMapper.FromReport(new MachineReport
        {
            Online = true,
            Overview =
            {
                ComputerName = "NW-DESK-102",
                Model = "OptiPlex 7010",
                SerialNumber = "9D2L8Q4",
                OsEdition = "Microsoft Windows 11 Pro",
                OsBuild = "22631.4317",
                Cpu = "Intel i5",
                RamBytes = 16L * 1024 * 1024 * 1024,
                LastBoot = new DateTimeOffset(2026, 10, 18, 8, 0, 0, TimeSpan.Zero),
                LoggedOnUser = @"NORTHWIND\m.owens"
            },
            Hardware = { Volumes = [new VolumeInfo { FreeBytes = 10L * 1024 * 1024 * 1024 }, new VolumeInfo { FreeBytes = 2L * 1024 * 1024 * 1024 }] }
        }, "typed");
        Assert.Equal("Yes", online.Online);
        Assert.Equal("OptiPlex 7010", online.Model);
        Assert.Equal("12.0 GB", online.DiskFree);
        Assert.Equal("16.0 GB", online.Ram);

        var offline = InventoryRowMapper.FromReport(new MachineReport { Online = false, Error = "Could not inventory OFFLINE-PC.", Overview = { ComputerName = "OFFLINE-PC" } }, "OFFLINE-PC");
        Assert.Equal("No", offline.Online);
        Assert.Equal("", offline.Model);
        Assert.Contains("Could not inventory", offline.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Scanner_PreservesOrderAndIsolatesFailures()
    {
        var collector = new DelegateCollector((request, _) =>
        {
            if (request.DisplayName == "BAD")
                throw new InvalidOperationException("disk query failed");
            return Task.FromResult(new MachineReport
            {
                Online = true,
                Overview = { ComputerName = request.DisplayName, Model = "M-" + request.DisplayName }
            });
        });
        var scanner = new InventoryScanner(collector, 4);
        var requests = new[] { "slow", "BAD", "fast" }.Select(Support.Request).ToList();
        var result = await scanner.ScanAsync(requests, null, CancellationToken.None);
        Assert.False(result.Cancelled);
        Assert.Equal(["slow", "BAD", "fast"], result.Rows.Select(r => r.Name).ToArray());
        Assert.Equal("No", result.Rows[1].Online);
        Assert.Contains("disk query failed", result.Rows[1].Detail, StringComparison.Ordinal);
        Assert.Equal("Yes", result.Rows[0].Online);
        Assert.Equal("Yes", result.Rows[2].Online);
    }

    [Fact]
    public async Task Scanner_LimitsParallelism()
    {
        var started = 0;
        var twoEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var collector = new DelegateCollector(async (request, token) =>
        {
            var count = Interlocked.Increment(ref started);
            if (count >= 2)
                twoEntered.TrySetResult();
            await gate.Task.WaitAsync(token);
            return new MachineReport { Online = true, Overview = { ComputerName = request.DisplayName } };
        });
        var scanner = new InventoryScanner(collector, 2);
        var requests = Enumerable.Range(0, 4).Select(i => Support.Request("PC" + i)).ToList();
        var scan = scanner.ScanAsync(requests, null, CancellationToken.None);
        await twoEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(100);
        Assert.Equal(2, Volatile.Read(ref started));
        gate.TrySetResult();
        var result = await scan.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(4, result.Rows.Count);
        Assert.All(result.Rows, row => Assert.Equal("Yes", row.Online));
    }

    [Fact]
    public async Task Scanner_CancelLeavesRemainingRows()
    {
        var started = 0;
        var collector = new DelegateCollector(async (request, token) =>
        {
            Interlocked.Increment(ref started);
            await Task.Delay(5000, token);
            return new MachineReport { Online = true, Overview = { ComputerName = request.DisplayName } };
        });
        var scanner = new InventoryScanner(collector, 1);
        using var cts = new CancellationTokenSource();
        var progress = new SyncProgress<ScanProgress>(_ => cts.Cancel());
        var result = await scanner.ScanAsync([Support.Request("A"), Support.Request("B"), Support.Request("C")], progress, cts.Token);
        Assert.True(result.Cancelled);
        Assert.Contains(result.Rows, row => row.Detail == "Cancelled");
        Assert.Equal(3, result.Rows.Count);
    }

    [Fact]
    public async Task Collector_AssemblesSummaryWithoutSoftware()
    {
        var session = new ScriptSession
        {
            OnQuery = (_, wql) =>
            {
                if (wql.Contains("Win32_ComputerSystem ", StringComparison.Ordinal))
                    return [Support.Row(("Name", "LAB"), ("Domain", "NORTHWIND.LOCAL"), ("PartOfDomain", true), ("Manufacturer", "HP"), ("Model", "Z2"), ("UserName", "lab"), ("TotalPhysicalMemory", 64L * 1024 * 1024 * 1024))];
                if (wql.Contains("Win32_BIOS", StringComparison.Ordinal))
                    return [Support.Row(("SerialNumber", "To be filled by O.E.M."), ("SMBIOSBIOSVersion", "1.2"), ("ReleaseDate", "20240409000000.000000+000"))];
                if (wql.Contains("Win32_ComputerSystemProduct", StringComparison.Ordinal))
                    return [Support.Row(("IdentifyingNumber", "HPZ2007NW"))];
                if (wql.Contains("Win32_OperatingSystem", StringComparison.Ordinal))
                    return [Support.Row(("Caption", "Microsoft Windows 11 Pro"), ("Version", "10.0.22631"), ("BuildNumber", "22631"), ("OSArchitecture", "64-bit"), ("InstallDate", "20240901100000.000000+000"), ("LastBootUpTime", "20261018080000.000000+000"), ("FreePhysicalMemory", 8L * 1024 * 1024))];
                if (wql.Contains("Win32_Processor", StringComparison.Ordinal))
                    return [Support.Row(("Name", "Intel Xeon"), ("NumberOfCores", 8), ("NumberOfLogicalProcessors", 16))];
                if (wql.Contains("Win32_LogicalDisk", StringComparison.Ordinal))
                    return [Support.Row(("DeviceID", "C:"), ("Size", 100L), ("FreeSpace", 40L), ("DriveType", 3), ("FileSystem", "NTFS"))];
                return Array.Empty<Wmi.WmiInstance>();
            }
        };
        var registry = new MemoryRegistry { Ubr = 4317, DisplayVersion = "23H2", SecureBoot = 1 };
        var collector = new MachineCollector(new FixedSessionFactory(session), new ListRegistryFactory(registry));
        var summary = await collector.CollectSummaryAsync(Support.Request("LAB"), CancellationToken.None);
        Assert.True(summary.Online);
        Assert.Equal("HPZ2007NW", summary.Overview.SerialNumber);
        Assert.Equal("22631.4317", summary.Overview.OsBuild);
        Assert.Contains("23H2", summary.Overview.OsVersion, StringComparison.Ordinal);
        Assert.Equal(SecureBootState.Enabled, summary.Overview.SecureBoot);
        Assert.Contains("Intel Xeon", summary.Overview.Cpu, StringComparison.Ordinal);
        Assert.NotEmpty(summary.Overview.Uptime?.ToString() ?? "");
        Assert.Empty(summary.Programs);
        Assert.DoesNotContain(session.Queries, q => q.Contains("Win32_Service", StringComparison.Ordinal));
        Assert.DoesNotContain(session.Queries, q => q.Contains("Win32_Product", StringComparison.Ordinal));
    }

    [Fact]
    public void RemoteRegistry_ReadsUninstallValues()
    {
        var session = new ScriptSession
        {
            OnInvoke = (method, args) =>
            {
                var path = args.TryGetValue("sSubKeyName", out var raw) ? Convert.ToString(raw) ?? "" : "";
                var outputs = new WmiInstance();
                if (method == "EnumKey")
                {
                    if (path.Equals(RegistryPaths.Uninstall, StringComparison.OrdinalIgnoreCase) && Equals(args["hDefKey"], WmiStdRegReader.HiveLocalMachine))
                    {
                        outputs.Set("ReturnValue", 0);
                        outputs.Set("sNames", new[] { "AppOne" });
                    }
                    else
                    {
                        outputs.Set("ReturnValue", 2);
                    }
                    return new WmiMethodResult { Outputs = outputs };
                }

                if (method == "GetStringValue" && path.EndsWith("AppOne", StringComparison.Ordinal))
                {
                    var valueName = Convert.ToString(args["sValueName"]);
                    outputs.Set("ReturnValue", valueName is "DisplayName" or "DisplayVersion" or "Publisher" or "InstallDate" ? 0 : 2);
                    outputs.Set("sValue", valueName switch
                    {
                        "DisplayName" => "Contoso Draw",
                        "DisplayVersion" => "1.2.0",
                        "Publisher" => "Contoso",
                        "InstallDate" => "20240115",
                        _ => ""
                    });
                    return new WmiMethodResult { Outputs = outputs };
                }

                if (method == "GetDWORDValue" && path.EndsWith("AppOne", StringComparison.Ordinal))
                {
                    var valueName = Convert.ToString(args["sValueName"]);
                    outputs.Set("ReturnValue", valueName == "EstimatedSize" ? 0 : 2);
                    outputs.Set("uValue", 4096);
                    return new WmiMethodResult { Outputs = outputs };
                }

                outputs.Set("ReturnValue", 2);
                return new WmiMethodResult { Outputs = outputs };
            }
        };
        var reader = new WmiStdRegReader(session);
        var records = reader.ReadUninstallRecords();
        var app = Assert.Single(records, r => r.DisplayName == "Contoso Draw");
        Assert.Equal("HKLM", app.Source);
        Assert.Equal(4096, app.EstimatedSizeKb);
        Assert.Equal("1.2.0", app.DisplayVersion);
        Assert.Empty(reader.Warnings);
    }

    [Fact]
    public void RemoteRegistry_RecordsInvokeFailures()
    {
        var session = new ScriptSession
        {
            OnInvoke = (_, _) => throw new InvalidOperationException("Access is denied.")
        };
        var reader = new WmiStdRegReader(session);
        Assert.Empty(reader.ReadUninstallRecords());
        Assert.Contains(reader.Warnings, warning => warning.Contains("Access is denied", StringComparison.Ordinal));
        Assert.Null(reader.ReadString("HKLM", RegistryPaths.WindowsCurrentVersion, "DisplayVersion"));
    }

    [Fact]
    public void WinRm_ParsesInstancesContextAndFaults()
    {
        const string xml = """
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope">
              <s:Body>
                <n:EnumerateResponse xmlns:n="http://schemas.xmlsoap.org/ws/2004/09/enumeration">
                  <n:EnumerationContext>ctx-1</n:EnumerationContext>
                  <n:Items>
                    <p:Win32_NetworkAdapterConfiguration>
                      <p:Description>Ethernet</p:Description>
                      <p:IPAddress>
                        <p:string>10.0.0.5</p:string>
                        <p:string>fe80::1</p:string>
                      </p:IPAddress>
                      <p:DHCPEnabled>true</p:DHCPEnabled>
                      <p:Empty xsi:nil="true" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" />
                      <p:Index>4</p:Index>
                    </p:Win32_NetworkAdapterConfiguration>
                  </n:Items>
                </n:EnumerateResponse>
              </s:Body>
            </s:Envelope>
            """;
        var page = WinRmProtocol.ParseResponse(xml);
        Assert.Equal("ctx-1", page.Context);
        Assert.False(page.EndOfSequence);
        var item = Assert.Single(page.Items);
        Assert.Equal("Ethernet", item.Text("Description"));
        Assert.Equal(["10.0.0.5", "fe80::1"], item.Texts("IPAddress"));
        Assert.True(item.Bool("DHCPEnabled"));
        Assert.Equal(4, item.Int32("Index"));
        Assert.Null(item.Get("Empty"));

        var fault = WinRmProtocol.ParseResponse("""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"><s:Body><s:Fault><s:Reason><s:Text>Access is denied.</s:Text></s:Reason></s:Fault></s:Body></s:Envelope>
            """);
        Assert.Equal("Access is denied.", fault.Fault);

        var done = WinRmProtocol.ParseResponse("""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"><s:Body><n:PullResponse xmlns:n="http://schemas.xmlsoap.org/ws/2004/09/enumeration"><n:Items /><n:EndOfSequence /></n:PullResponse></s:Body></s:Envelope>
            """);
        Assert.True(done.EndOfSequence);
    }

    [Fact]
    public void WinRm_BuildsEnvelopeAndReadsMethodOutput()
    {
        Assert.True(WinRmProtocol.TryClassFromWql("SELECT Name FROM Win32_OperatingSystem WHERE Primary = TRUE", out var className));
        Assert.Equal("Win32_OperatingSystem", className);
        Assert.Contains("root/cimv2/Win32_OperatingSystem", WinRmProtocol.ResourceUri(@"root\cimv2", className), StringComparison.Ordinal);
        var envelope = WinRmProtocol.BuildEnumerate("http://pc:5985/wsman", WinRmProtocol.ResourceUri(@"root\cimv2", className), "SELECT Name FROM Win32_OperatingSystem WHERE Name = '<x>'", "abc");
        Assert.Contains(WinRmProtocol.WqlDialect, envelope, StringComparison.Ordinal);
        Assert.Contains("&lt;x&gt;", envelope, StringComparison.Ordinal);
        Assert.Contains("uuid:abc", envelope, StringComparison.Ordinal);

        var result = WinRmProtocol.ParseMethodResult("""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"><s:Body>
              <p:GetStringValue_OUTPUT><p:ReturnValue>0</p:ReturnValue><p:sValue>23H2</p:sValue></p:GetStringValue_OUTPUT>
            </s:Body></s:Envelope>
            """);
        Assert.Equal(0, result.ReturnValue);
        Assert.Equal("23H2", result.Outputs.Text("sValue"));
        Assert.Throws<InvalidOperationException>(() => WinRmProtocol.ParseMethodResult("""
            <s:Envelope xmlns:s="http://www.w3.org/2003/05/soap-envelope"><s:Body><s:Fault><s:Text>Access is denied.</s:Text></s:Fault></s:Body></s:Envelope>
            """));
    }

    [Fact]
    public void ByteQuantity_SortsBySizeNotText()
    {
        var small = new ByteQuantity(512L * 1024 * 1024);
        var large = new ByteQuantity(2L * 1024 * 1024 * 1024);
        Assert.True(small.CompareTo(large) < 0);
        Assert.Equal("512.0 MB", small.ToString());
        Assert.Equal("2.0 GB", large.ToString());
    }
}

public class DemoPipelineTests
{
    [Fact]
    public async Task DemoPrimary_FillsOverviewHardwareAndSoftware()
    {
        var collector = new MachineCollector(new DemoWmiSessionFactory(), new DemoRegistryFactory());
        var report = await collector.CollectAsync(new CollectRequest(new ConnectionTarget("", null, null), DemoCatalog.PrimaryName), CancellationToken.None);
        Assert.True(report.Online);
        Assert.Equal("NW-LAP-014", report.Overview.ComputerName);
        Assert.Equal("Latitude 7450", report.Overview.Model);
        Assert.Equal("7F3K2C1", report.Overview.SerialNumber);
        Assert.Equal("Dell Inc.", report.Overview.Manufacturer);
        Assert.Equal("22631.4317", report.Overview.OsBuild);
        Assert.Contains("23H2", report.Overview.OsVersion, StringComparison.Ordinal);
        Assert.Equal(SecureBootState.Enabled, report.Overview.SecureBoot);
        Assert.Equal("Present (2.0)", DisplayFormatter.Tpm(report.Overview.TpmPresent, report.Overview.TpmVersion));
        Assert.Contains("Ultra 7", report.Overview.Cpu, StringComparison.Ordinal);
        Assert.Equal(@"NORTHWIND\a.chen", report.Overview.LoggedOnUser);
        Assert.True(report.Overview.Uptime is { } uptime && uptime > TimeSpan.FromHours(20));
        Assert.Contains(report.Overview.BitLocker, b => b.DriveLetter == "C:" && b.Status == "On");
        Assert.Contains(report.Overview.Adapters, a => a.IpAddresses.Contains("10.20.14.42", StringComparison.Ordinal));
        Assert.Equal(2, report.Hardware.MemoryModules.Count);
        Assert.Equal("Healthy", report.Hardware.Disks[0].Health);
        Assert.Equal("SSD", report.Hardware.Disks[0].MediaType);
        Assert.Equal("CN0NW14L7440", report.Hardware.Monitors[0].Serial);
        Assert.True(report.Hardware.Battery.Present);
        Assert.Equal(86.1, report.Hardware.Battery.HealthPercent);
        Assert.Contains(report.Programs, p => p.Name == "Northwind Endpoint Agent");
        Assert.Contains(report.Programs, p => p.Name == "Slack" && p.Source == "HKCU");
        Assert.Contains(report.Programs, p => p.Name == "Legacy Label Printer" && p.Source == "HKLM (32-bit)");
        Assert.Contains(report.Programs, p => p.Name == "Dell Command Update");
        Assert.DoesNotContain(report.Programs, p => p.Name.StartsWith("Update for", StringComparison.Ordinal));
        Assert.DoesNotContain(report.Programs, p => p.Name == "Microsoft Update Health Tools");
        Assert.Equal("KB5044285", report.Hotfixes[0].HotFixId);
        Assert.Contains(report.Services, s => s.Name == "WinRM" && s.Status == "Running");
        Assert.Contains(report.StartupItems, s => s.Name == "Spotify" && s.Enabled == "No");
        Assert.Contains(report.StartupItems, s => s.Name == "Northwind Agent");
        Assert.Equal("", report.Error);
    }

    [Fact]
    public async Task DemoInventory_VariesMachinesAndExplainsBlocks()
    {
        var collector = new MachineCollector(new DemoWmiSessionFactory(), new DemoRegistryFactory());
        var scanner = new InventoryScanner(collector, 4);
        var requests = DemoCatalog.SampleNames.Select(name => new CollectRequest(new ConnectionTarget(name, null, null), name)).ToList();
        var result = await scanner.ScanAsync(requests, null, CancellationToken.None);
        Assert.Equal(DemoCatalog.SampleNames.Count, result.Rows.Count);
        Assert.Equal("Yes", result.Rows.Single(r => r.Name == "NW-LAP-014").Online);
        Assert.Contains("Latitude", result.Rows.Single(r => r.Name == "NW-LAP-014").Model, StringComparison.Ordinal);
        Assert.Equal("Microsoft Windows 10 Pro", result.Rows.Single(r => r.Name == "NW-DESK-118").Os);
        Assert.Equal("No", result.Rows.Single(r => r.Name == "OFFLINE-PC").Online);
        Assert.Contains("RPC server is unavailable", result.Rows.Single(r => r.Name == "OFFLINE-PC").Detail, StringComparison.Ordinal);
        Assert.Contains("Access was denied", result.Rows.Single(r => r.Name == "BLOCKED-PC").Detail, StringComparison.Ordinal);
        Assert.Contains("WinRM", result.Rows.Single(r => r.Name == "BLOCKED-PC").Detail, StringComparison.Ordinal);
        var serials = result.Rows.Where(r => r.Online == "Yes").Select(r => r.Serial).ToList();
        Assert.Equal(serials.Count, serials.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public async Task DemoUnknownName_StillReturnsAMachine()
    {
        var collector = new MachineCollector(new DemoWmiSessionFactory(), new DemoRegistryFactory());
        var report = await collector.CollectSummaryAsync(Support.Request("WAREHOUSE-42"), CancellationToken.None);
        Assert.True(report.Online);
        Assert.Equal("WAREHOUSE-42", report.Overview.ComputerName);
        Assert.False(string.IsNullOrWhiteSpace(report.Overview.Model));
        Assert.False(string.IsNullOrWhiteSpace(report.Overview.SerialNumber));
    }

    [Fact]
    public async Task DemoDesktop_HasNoBattery()
    {
        var collector = new MachineCollector(new DemoWmiSessionFactory(), new DemoRegistryFactory());
        var report = await collector.CollectAsync(Support.Request("NW-DESK-102"), CancellationToken.None);
        Assert.False(report.Hardware.Battery.Present);
        Assert.Equal("OptiPlex 7010", report.Overview.Model);
        Assert.Contains(report.Hardware.Gpus, g => g.Name.Contains("RTX 4060", StringComparison.Ordinal));
    }
}
