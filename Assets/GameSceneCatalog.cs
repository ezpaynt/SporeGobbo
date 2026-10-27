using UnityEngine.SceneManagement;
/// <summary>Immutable normal V1 scene roles. Existing lifecycle services perform transitions.</summary>
public static class GameSceneCatalog
{
 public const string MainMenu="MainMenu";
 public const string CandidateCamp="FutureCampCandidate",CandidateExpedition="FirstExpedition",CandidateIntro="FirstGobboIntro";
 public static string Camp=>CandidateCamp;
 public static string Run=>CandidateExpedition;
 public static string Intro=>CandidateIntro;
 public static string ResolveIntro(string requested)=>Intro;
 public static string Resolve(string requested)=>requested=="CampScene"||requested==CandidateCamp?Camp:requested;
 public static bool IsCamp(Scene scene)=>scene.name==CandidateCamp||scene.name=="CampScene";
 public static bool IsExpedition(Scene scene)=>scene.name==CandidateExpedition;
 public static bool IsIntro(Scene scene)=>scene.name==CandidateIntro;
 // Historical proof APIs remain source-compatible; they never activate normal game correctness/routing.
 public static bool DevelopmentCandidate=>V1RuntimeProfile.DevelopmentToolsEnabled;
 public static bool ExpeditionEnabled=>true;
 public static bool AuthoredIntroEnabled=>true;
 public static void EnableDevelopmentCandidate()=>V1RuntimeProfile.EnableLegacyDevelopmentTools();
 public static void EnableExpedition(){}
 public static void EnableAuthoredIntro(){}
}
