#if UNITY_EDITOR
using SporeGobbo.CampLifecycle;using System;using System.Collections.Generic;using System.Reflection;using NUnit.Framework;using UnityEditor;using UnityEngine;
// Owned real-prefab contact/commit contracts. Explicit component ticks are an EditMode adapter,
// not evidence of natural AI outcomes. No GameState, slots, scenes, death or file/prefs writes.
public sealed class Stage64BPostAuditTests
{
 const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
 readonly List<GameObject> owned=new List<GameObject>();
 sealed class Floor:IDiggableTerrain
 {
  public float CellSize=>.6f;public bool IsBlocked(Vector2Int c)=>false;public bool IsDiggable(Vector2Int c)=>false;
  public Vector2Int WorldToCell(Vector2 p)=>Vector2Int.FloorToInt(p/.6f);public Vector2 CellToWorld(Vector2Int c)=>(Vector2)c*.6f;
  public void DigCircle(Vector2 p,float r){}public TerrainDigResult DigCircle(Vector2 p,float r,TerrainDigAuthority a,int s,IReadOnlyCollection<Vector2Int> c)=>new TerrainDigResult(0,0,0,TerrainDigFailureReason.NoDirtRemoved);
 }
 readonly Floor floor=new Floor();
 GameObject Prefab(string path){var p=AssetDatabase.LoadAssetAtPath<GameObject>(path);Assert.That(p,Is.Not.Null);var go=UnityEngine.Object.Instantiate(p);owned.Add(go);var binding=go.AddComponent<ActorTerrainBinding>();binding.Bind(floor);binding.Ready=()=>true;return go;}
 static void Call(object host,string method)=>host.GetType().GetMethod(method,Hidden).Invoke(host,null);
 static void Set(object host,string field,object value)=>host.GetType().GetField(field,Hidden).SetValue(host,value);
 [TearDown]public void Cleanup(){for(int i=owned.Count-1;i>=0;i--)if(owned[i]!=null)UnityEngine.Object.DestroyImmediate(owned[i]);owned.Clear();Physics2D.SyncTransforms();}
 (BuddyCombat combat,Collider2D body,BlobSpitterEnemy blob,Collider2D target) Pair(bool grown,int direction,float gap)
 {
  var gobbo=Prefab("Assets/GobboBuddy.prefab");var unit=gobbo.GetComponent<BuddyUnit>();unit.Initialize(new GobboUnitSaveData{uniqueId="owned-contact",health=100,maxHealth=100,gobboType=grown?BuddyType.Tank:BuddyType.Baby,ageStage=grown?GobboAgeStage.Stage4:GobboAgeStage.Baby});
  var combat=gobbo.GetComponent<BuddyCombat>();Call(combat,"Awake");combat.onlyFightNearPlayer=false;combat.brainAllowsMovement=true;
  var other=Prefab("Assets/BlobSpitter.prefab");var blob=other.GetComponent<BlobSpitterEnemy>();blob.health=100;blob.spriteRenderer=null;
  var a=gobbo.GetComponent<Collider2D>();var b=other.GetComponent<Collider2D>();Assert.That(a.isTrigger||b.isTrigger,Is.False);
  gobbo.transform.position=Vector3.zero;other.transform.position=Vector3.zero;Physics2D.SyncTransforms();Vector2 axis=direction==0?Vector2.right:direction==1?Vector2.left:direction==2?Vector2.up:Vector2.down;
  float separation=(direction<2?a.bounds.extents.x+b.bounds.extents.x:a.bounds.extents.y+b.bounds.extents.y)+gap;other.transform.position=axis*separation;other.GetComponent<Rigidbody2D>().position=other.transform.position;Physics2D.SyncTransforms();
  Set(combat,"currentTarget",other.transform);return(combat,a,blob,b);
 }
 [TestCase(false,0)][TestCase(false,1)][TestCase(false,2)][TestCase(false,3)]
 [TestCase(true,0)][TestCase(true,1)][TestCase(true,2)][TestCase(true,3)]
 public void ActualRunPrefabAtPhysicalContactCanCommitBite(bool grown,int direction)
 {
  var p=Pair(grown,direction,.005f);Assert.That(Vector2.Distance(p.combat.transform.position,p.blob.transform.position),Is.GreaterThan(p.combat.attackRange));var distance=p.body.Distance(p.target);Assert.That(distance.isValid&&distance.distance<.02f,Is.True);
  Call(p.combat,"FixedUpdate");Assert.That(p.blob.health,Is.LessThan(100),"Real attack owner failed to commit at actual body contact; explicit fixed tick adapter");
 }
 [TestCase(false,0)][TestCase(true,0)][TestCase(true,2)]
 public void ContactCorrectionMustNotGrantNewSurfaceAttackRange(bool grown,int direction)
 {var p=Pair(grown,direction,.1f);Call(p.combat,"FixedUpdate");Assert.That(p.blob.health,Is.EqualTo(100));}
 [Test]public void TargetMovedBeforeCommitDoesNotConsumeCooldownOrHit()
 {var p=Pair(false,0,.005f);p.blob.transform.position=Vector2.right*3;p.blob.GetComponent<Rigidbody2D>().position=p.blob.transform.position;Physics2D.SyncTransforms();Call(p.combat,"TryAttack");Assert.That(p.blob.health,Is.EqualTo(100));Assert.That((float)typeof(BuddyCombat).GetField("attackTimer",Hidden).GetValue(p.combat),Is.Zero);}
 [Test]public void TriggerContactCannotExtendAttackReach()
 {var p=Pair(true,0,.005f);p.body.isTrigger=true;Call(p.combat,"FixedUpdate");Assert.That(p.blob.health,Is.EqualTo(100));}
}
#endif

