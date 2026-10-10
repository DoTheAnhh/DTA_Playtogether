using System.Diagnostics;
using System.Text;
using DTA.Game.Session;
using DTA.Runtime.Device;

Console.OutputEncoding = Encoding.UTF8;
DTA.Runtime.Core.Log.MinLevel = DTA.Runtime.Core.LogLevel.Debug;
var sw = Stopwatch.StartNew();
void Step(string text) => Console.WriteLine($"[{sw.ElapsedMilliseconds,6} ms] {text}");

if (args is ["payload"])
{
    PayloadCheck.Run(Step);
    return;
}

if (args is ["adbsock", var ser]) { AdbSocket.Run(ser, Step); return; }
var instances = Emulators.All.SelectMany(e => e.Instances()).ToList();
var instance = instances.FirstOrDefault(i => args.Length == 0 || i.Serial == args[0]) ?? throw new Exception("Không thấy tab nào");
var session = SessionHub.Get(EmulatorDevice.Of(instance), Step);
if (args is [_, "run", var feature, var secs]) { BotRun.Run(EmulatorDevice.Of(instance), feature, int.Parse(secs), Step); SessionHub.CloseAll(); return; }
if (args is [_, "tele", var tx, var ty, var tz, ..] rest) { DTA.Engine.Core.Risk.Install(); DTA.Engine.Core.Risk.Set(DTA.Engine.Core.RiskFeature.Teleport, true); var yaw = rest.Length > 5 ? double.Parse(rest[5], System.Globalization.CultureInfo.InvariantCulture) * Math.PI / 360 : double.NaN; var r = session.Teleport.Go(new DTA.Game.Actor.TeleportRequest(new DTA.Game.Geometry.Vec3(float.Parse(tx, System.Globalization.CultureInfo.InvariantCulture), float.Parse(ty, System.Globalization.CultureInfo.InvariantCulture), float.Parse(tz, System.Globalization.CultureInfo.InvariantCulture)), Rotation: double.IsNaN(yaw) ? null : new DTA.Game.Geometry.Quat(0, (float)Math.Sin(yaw), 0, (float)Math.Cos(yaw)), AlignCamera: true), Step); Step($"Kết quả: {r}"); SessionHub.CloseAll(); return; }
if (args is [_, "fishwatch", var ws]) { FishWatch.Run(session, int.Parse(ws), Step); SessionHub.CloseAll(); return; }
if (args is [_, "results", var rs]) { ResultWatch.Run(session, int.Parse(rs), Step); SessionHub.CloseAll(); return; }
if (args is [_, "net", var ns]) { NetCheck.Run(session, int.Parse(ns), Step); SessionHub.CloseAll(); return; }
if (args is [_, "capsule", var cs]) { CapsuleWatch.Run(session, int.Parse(cs), Step); SessionHub.CloseAll(); return; }
if (args is [_, "esp", var es]) { EspRun.Run(EmulatorDevice.Of(instance), int.Parse(es), Step); SessionHub.CloseAll(); return; }
if (args is [_, "bench"]) { Bench.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "digonce", var dn, var dg]) { DigOnce.Run(session, int.Parse(dn), int.Parse(dg), Step); SessionHub.CloseAll(); return; }
if (args is [_, "spots"]) { SpotList.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "flags"]) { CollectFlags.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "zones"]) { ZoneTable.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "mutations"]) { MutationTable.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "code", var cr, var cn]) { CodeDump.Run(session, Convert.ToInt64(cr, 16), int.Parse(cn), Step); SessionHub.CloseAll(); return; }
if (args is [_, "travel", var tm]) { Step($"Đổi map: {session.Teleport.Travel(int.Parse(tm), Step)}"); SessionHub.CloseAll(); return; }
if (args is [_, "zoneobj"]) { ZoneObjects.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "hier"]) { HierDump.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "special"]) { foreach (var z in new DTA.Features.Fishing.FishingGame(session).SpecialZoneIds()) Step($"{z.Asset} -> {z.Id}"); SessionHub.CloseAll(); return; }
if (args is [_, "allzones"]) { AllZones.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "zonelist"]) { foreach (var z in DTA.Features.Fishing.FishingBot.KnownZones(session.Cache).OrderBy(z => int.Parse(z.Key))) Step($"{z.Key} - {z.Value}"); SessionHub.CloseAll(); return; }
if (args is [_, "fakecheck"]) { FakeCheck.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "cachepoll", var cp]) { CachePoll.Run(session, int.Parse(cp), Step); SessionHub.CloseAll(); return; }
if (args is [_, "where"]) { Step($"addr {session.System("sysFishing"):X} {session.Control:X} {session.Camera.CurrentMap():X} {session.System("sysCollect"):X} {session.Managed.ClassOf(session.System("sysFishing")):X}"); SessionHub.CloseAll(); return; }
if (args is [_, "zclass"]) { foreach (var n in new[] { "FishingZoneInfo", "FishingZone.FishingZoneInfo", "FishingZone/FishingZoneInfo", "FishingZone" }) Step($"{n}: 0x{session.Class(n):X}"); SessionHub.CloseAll(); return; }
if (args is [_, "treezones"]) { TreeZones.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "infoclass", var zhex]) { var z = Convert.ToInt64(zhex, 16); var info = session.Managed.ListItems(session.Managed.Ptr(z, "FishingZoneList"))[0]; Step($"FishingZoneInfo class 0x{session.Managed.ClassOf(info):X} info 0x{info:X} idAt {session.Il2Cpp.Field(session.Managed.ClassOf(info), "FishingZoneID")}"); SessionHub.CloseAll(); return; }
if (args is [_, "peek", var pa, var pl]) { var d = session.Memory.Read(Convert.ToInt64(pa, 16), int.Parse(pl)); Step(d == null ? "null" : System.Text.Encoding.ASCII.GetString(d.Select(b => b is >= 32 and < 127 ? b : (byte)46).ToArray())); SessionHub.CloseAll(); return; }
if (args is [_, "classregions"]) { var ks = new[] { session.ControlClass, session.Managed.ClassOf(session.System("sysFishing")), session.Managed.ClassOf(session.Camera.CurrentMap()), session.Managed.ClassOf(session.System("sysCollect")), session.Managed.ClassOf(session.Character()) }; foreach (var k in ks) { var r = session.Il2Cpp.Anon.FirstOrDefault(a => k >= a.Start && k < a.End); Step($"class 0x{k:X} {session.Il2Cpp.FullName(k)} -> 0x{r.Start:X}-0x{r.End:X} {(r.End - r.Start) >> 20} MB"); } SessionHub.CloseAll(); return; }
if (args is [_, "water", ..] wa) { var col = wa.Length > 2 ? Convert.ToInt64(wa[2], 16) : session.Managed.Ptr(session.Control, "_waterCollider"); Step($"_waterCollider 0x{col:X} {session.Il2Cpp.FullName(session.Managed.ClassOf(col))}"); if (col != 0) { var same = session.World.Scripts([col]).Values.SelectMany(d => d); Step("cùng GO: " + string.Join(", ", same.Select(p => p.Key))); var whole = session.World.DescendantScripts(col, true); Step($"cả cây: {whole.Count} script, FishingZone {whole.Count(x => x.Class == "FishingZone")}, FisheryZoneTrigger {whole.Count(x => x.Class == "FisheryZoneTrigger")}"); foreach (var (_, ft) in whole.Where(x => x.Class == "FisheryZoneTrigger")) Step($"  FisheryZoneTrigger 0x{ft:X}: ID {session.Managed.I32(ft, "FishingZoneID")} @ {session.World.Position(ft)}"); foreach (var (_, wz) in whole.Where(x => x.Class == "FishingZone").Take(40)) Step($"  zone 0x{wz:X}: {string.Join(", ", session.Managed.ListItems(session.Managed.Ptr(wz, "FishingZoneList")).Select(i => session.Managed.I32(i, "FishingZoneID")))}"); foreach (var (cls, z) in session.World.AncestorScripts(col).Where(x => x.Class == "FishingZone").Concat(same.Where(p => p.Key == "FishingZone").Select(p => (p.Key, p.Value)))) Step($"FishingZone 0x{z:X}: {string.Join(", ", session.Managed.ListItems(session.Managed.Ptr(z, "FishingZoneList")).Select(i => session.Managed.I32(i, "FishingZoneID")))}"); } SessionHub.CloseAll(); return; }
if (args is [_, "floatwater"]) { var fl = new DTA.Features.Fishing.FishingGame(session).Float(); var tw = fl != 0 ? session.Managed.Ptr(fl, "_targetWaterCol") : 0; Step($"phao 0x{fl:X} _targetWaterCol 0x{tw:X} {session.Il2Cpp.FullName(session.Managed.ClassOf(tw))}"); if (tw != 0) { Step("cùng GO + cha: " + string.Join(", ", session.World.AncestorScripts(tw).Select(x => x.Class))); foreach (var (_, z) in session.World.AncestorScripts(tw).Where(x => x.Class == "FishingZone")) Step($"FishingZone 0x{z:X}: {string.Join(", ", session.Managed.ListItems(session.Managed.Ptr(z, "FishingZoneList")).Select(i => $"{session.Managed.I32(i, "FishingZoneID")} @0x{i:X}"))}"); } SessionHub.CloseAll(); return; }
if (args is [_, "floatpoll", var fp]) { FloatWater.Run(session, int.Parse(fp), Step); SessionHub.CloseAll(); return; }
if (args is [_, "dumptest", var dmb]) { var r = session.Il2Cpp.Anon.First(a => session.ControlClass >= a.Start && session.ControlClass < a.End); var sw1 = System.Diagnostics.Stopwatch.StartNew(); var d = DTA.Runtime.Memory.MemoryDump.Dump(session.Memory, session.Device, r.Start, Math.Min(r.End, r.Start + int.Parse(dmb) * 1048576L)); Step($"chép {d.Length >> 20} MB trong {sw1.ElapsedMilliseconds} ms"); SessionHub.CloseAll(); return; }
if (args is [_, "heapzones"]) { HeapZones.Run(session, Step); SessionHub.CloseAll(); return; }
if (args is [_, "faketest", var fz, var fs]) { FakeTest.Run(session, int.Parse(fz), int.Parse(fs), Step); SessionHub.CloseAll(); return; }
if (args is [_, "dtype"]) { var top = session.Open.Top(); var info = top.Addr != 0 ? session.Managed.Ptr(top.Addr, "m_DialogInfo") : 0; Step($"bảng {top.Name} 0x{top.Addr:X} m_DialogType = {(info != 0 ? session.Managed.I32(info, "m_DialogType") : null)}"); SessionHub.CloseAll(); return; }
if (args is [_, "dialogs"]) { var ds = session.System("sysDialog"); foreach (var (k, d) in session.Managed.DictItems(session.Managed.Ptr(ds, "_instanceDialogs")).Take(40)) { var inf = session.Managed.Ptr(d, "m_DialogInfo"); Step($"{session.Il2Cpp.FullName(session.Managed.ClassOf(d))} type {(inf != 0 ? session.Managed.I32(inf, "m_DialogType") : null)}"); } SessionHub.CloseAll(); return; }
if (args is [_, "base"]) { BaseCheck.Run(session, Step); SessionHub.CloseAll(); return; }
Step($"Phiên: pid {session.Process.Pid}, điều khiển {session.Il2Cpp.FullName(session.ControlClass)}");
Step($"Bản đồ: {session.Camera.Map()}");
Step($"Nhân vật: {session.Player.Pose()?.Position}, motion {session.Player.Motion()}");
Step($"Đứng vững: {session.Player.Grounded()}, phương tiện: {session.Vehicles.Current()}");
Step($"Camera: {session.Camera.View()}");
Step($"Cần điều khiển: {session.Ui.Joystick()}, nút reel {session.Ui.HudButton("reel")}");
Step($"Bảng trên cùng: {session.Open.Top().Name}");
var things = session.Map.Things();
Step($"Vật thể: {things.Count} - {string.Join(", ", things.Take(3).Select(t => $"{t.Asset}@({t.X:F1},{t.Z:F1})"))}");
var insects = session.Map.Insects();
Step($"Côn trùng: {insects.Count}");
Step($"Bảng vật phẩm: {session.Tables.Items().Count} dòng");
foreach (var bug in insects.Take(3)) Step($"  {session.Tables.Item(bug.Item)} giá {session.Tables.Price(bug.Item)}");
if (session.Player.Position() is { } here) Step($"Mặt đất dưới chân: {session.Ground.Resolve(here.X, here.Z, here.Y)}");
Step($"Số cửa: {session.Camera.DoorCount()}");
Step($"Dụng cụ cần câu: {session.Held.Read(DTA.Game.Data.Tools.Rod)}");
SessionHub.CloseAll();
Step("Xong");
