using System;
using UnityEngine;
using UnityEngine.SceneManagement;
/// <summary>Destinations/roles only. Existing lifecycle services remain the scene loaders.</summary>
public static class GameSceneCatalog
{
 public const string CandidateCamp="FutureCampCandidate";
 public static bool DevelopmentCandidate{get;private set;}
 public static string Camp=>DevelopmentCandidate?CandidateCamp:"CampScene";
 public const string CandidateExpedition="FirstExpedition";
 public static string Run=>DevelopmentCandidate&&ExpeditionEnabled?CandidateExpedition:"SampleScene";
 public static bool ExpeditionEnabled{get;private set;}
 public static void EnableExpedition(){if(!SporeGobbo.Safety.Persistence.Isolated)throw new InvalidOperationException("Expedition requires isolated storage");ExpeditionEnabled=true;}
 public const string CandidateIntro="FirstGobboIntro";
 public static bool AuthoredIntroEnabled{get;private set;}
 public static string Intro=>DevelopmentCandidate&&AuthoredIntroEnabled?CandidateIntro:"SampleScene";
 public static void EnableAuthoredIntro(){if(!DevelopmentCandidate||!SporeGobbo.Safety.Persistence.Isolated)throw new InvalidOperationException("Intro requires candidate storage");AuthoredIntroEnabled=true;}
 public static string ResolveIntro(string requested)=>DevelopmentCandidate&&AuthoredIntroEnabled?Intro:requested;
 public static string Resolve(string requested)=>requested=="CampScene"?Camp:requested;
 public static bool IsCamp(Scene scene)=>scene.name=="CampScene"||DevelopmentCandidate&&scene.name==CandidateCamp;
 public static void EnableDevelopmentCandidate()
 {if(!SporeGobbo.Safety.Persistence.Isolated)throw new InvalidOperationException("Candidate routing requires owned development storage");SporeGobbo.Safety.Persistence.ExperimentPath("Stage61");DevelopmentCandidate=true;}
 [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Reset(){DevelopmentCandidate=false;ExpeditionEnabled=false;AuthoredIntroEnabled=false;}
}
