#if UNITY_EDITOR
using System;
using System.IO;
using NUnit.Framework;
using SporeGobbo.Safety;
public class PersistenceSafetyTests
{
 [Test] public void SandboxSaveRoundTripUsesProductionSerializer()
 {
  var data = new SporeSaveSlotData { slotIndex=3, playerName="Synthetic safety sentinel" };
  SporeSaveManager.SaveSlot(data);
  Assert.That(SporeSaveManager.LoadSlot(3).playerName,Is.EqualTo(data.playerName));
  Assert.That(SporeSaveManager.GetSlotPath(3).StartsWith(Environment.GetEnvironmentVariable("SPORE_TEST_ROOT")),Is.True);
 }
 [Test] public void MissingRootFailsClosed()
 {
  string old=Environment.GetEnvironmentVariable("SPORE_TEST_ROOT");
  try { Environment.SetEnvironmentVariable("SPORE_TEST_ROOT",null); Assert.Throws<InvalidOperationException>(()=>SporeSaveManager.GetSlotPath(1)); Assert.Throws<InvalidOperationException>(()=>Persistence.GetInt("x")); }
  finally { Environment.SetEnvironmentVariable("SPORE_TEST_ROOT",old); }
 }
 [TestCase("../sibling/save.json")] [TestCase("../../save.json")] [TestCase("C:/production/save.json")] [TestCase(".")]
 public void EscapeRejected(string path) { Assert.Throws<IOException>(()=>Persistence.Within(Environment.GetEnvironmentVariable("SPORE_TEST_ROOT"),path)); }
 [Test] public void PreferencesStayInMemory()
 {
  string key="SafetySynthetic_"+Guid.NewGuid().ToString("N");
  Persistence.SetInt(key,47); Persistence.Save();
  Assert.That(Persistence.GetInt(key),Is.EqualTo(47)); Assert.That(UnityEngine.PlayerPrefs.HasKey(key),Is.False);
  Persistence.DeleteKey(key); Assert.That(Persistence.HasKey(key),Is.False);
 }
 [Test] public void LinkedOrUnownedRootRejected()
 {
  string old=Environment.GetEnvironmentVariable("SPORE_TEST_ROOT");
  try { Environment.SetEnvironmentVariable("SPORE_TEST_ROOT",Path.GetTempPath()); Assert.Throws<InvalidOperationException>(()=>SporeSaveManager.GetSlotPath(1)); }
  finally { Environment.SetEnvironmentVariable("SPORE_TEST_ROOT",old); }
 }
 [Test] public void JunctionCannotRedirectSaveOrCleanupTarget()
 {
  string root=Environment.GetEnvironmentVariable("SPORE_TEST_ROOT");
  string outside=Path.Combine(root,"sentinel-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(outside);
  string sentinel=Path.Combine(outside,"sentinel.txt"); File.WriteAllText(sentinel,"unchanged");
  string link=Path.Combine(root,"junction-"+Guid.NewGuid().ToString("N"));
  var start=new System.Diagnostics.ProcessStartInfo("cmd.exe", "/c mklink /J \""+link+"\" \""+outside+"\"") { CreateNoWindow=true, UseShellExecute=false, RedirectStandardOutput=true, RedirectStandardError=true };
  using(var process=System.Diagnostics.Process.Start(start)) { process.WaitForExit(); if(process.ExitCode!=0) Assert.Ignore("Junction creation unavailable"); }
  Assert.Throws<IOException>(()=>Persistence.Within(root,Path.GetFileName(link)+"/slot_1.json"));
  Assert.That(File.ReadAllText(sentinel),Is.EqualTo("unchanged"));
 }
 [Test] public void PinnedEditorTestStateApiIsAvailable()
 {
  var api=Type.GetType("UnityEditor.TestTools.TestRunner.Api.TestRunnerApi, UnityEditor.TestRunner",true);
  var method=api.GetMethod("IsRunActive",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Static);
  Assert.That(method,Is.Not.Null); Assert.That((bool)method.Invoke(null,null),Is.True);
 }
 [Test] public void IndirectDeathAutosaveWritesOnlySyntheticSlot()
 {
  if(GameState.Instance!=null) UnityEngine.Object.DestroyImmediate(GameState.Instance.gameObject);
  var host=new UnityEngine.GameObject("SafetySyntheticState");
  try
  {
   var state=host.AddComponent<GameState>(); state.EnsureRuntimeDefaults();
   var unit=new GobboUnitSaveData { uniqueId="safety-synthetic-buddy", displayName="Synthetic Buddy" };
   state.AddGobbo(unit,false);
   SporeSaveManager.SaveSlot(new SporeSaveSlotData {slotIndex=1,playerName="Synthetic campaign"});
   string path=SporeSaveManager.GetSlotPath(1); string before=File.ReadAllText(path);
   state.RegisterGobboDeath(state.FindOwnedGobbo(unit.uniqueId));
   Assert.That(File.ReadAllText(path),Is.Not.EqualTo(before));
   Assert.That(path.StartsWith(Environment.GetEnvironmentVariable("SPORE_TEST_ROOT")),Is.True);
   // Match the legacy EditMode path: Awake has not registered this synthetic state.
   Assert.That(SporeSaveManager.LoadSlot(1).hasSave,Is.True);
  }
  finally { UnityEngine.Object.DestroyImmediate(host); }
 }
}
#endif
