using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace LobbyImprovements
{
	[HarmonyPatch]
	public class VersionDiffs
	{
		private static bool checkedVersion;
        
		private static string rootPath = "D:\\Documents\\Coding\\Discord\\LogiBot-Backend\\diffs\\repo";
		private static string batchFilePath = "D:\\Documents\\Coding\\Diffs\\repo\\auto-generate-diffs.bat";
        
		[HarmonyPatch(typeof(SteamManager), "Start")]
		[HarmonyPostfix]
		[HarmonyWrapSafe]
		private static void BuildManager_Awake()
		{
			if (checkedVersion || !BuildManager.instance || !Directory.Exists(rootPath)) return;

			Version newVersion = BuildManager.instance.version;
			string exportPath = $"{rootPath}/{newVersion.title}";
			if (!Directory.Exists(exportPath)) Directory.CreateDirectory(exportPath);
			
			string changelogPath = $"{exportPath}/changelog.md";
			if (!File.Exists(changelogPath))
			{
				string changelogString = "";
				if (newVersion.newList.Count > 0) changelogString += $"\n\n## NEW\n- {string.Join("\n- ", newVersion.newList.OrderBy(x => x))}";
				if (newVersion.changesList.Count > 0) changelogString += $"\n\n## CHANGES\n- {string.Join("\n- ", newVersion.changesList.OrderBy(x => x))}";
				if (newVersion.balancingList.Count > 0)  changelogString += $"\n\n## BALANCING\n- {string.Join("\n- ", newVersion.balancingList.OrderBy(x => x))}";
				if (newVersion.fixList.Count > 0) changelogString += $"\n\n## FIXES\n- {string.Join("\n- ", newVersion.fixList.OrderBy(x => x))}";
				File.WriteAllText($"{exportPath}/changelog.md", changelogString.Trim());
			}

			string lastVersion = "";
			if (File.Exists($"{rootPath}/versions.json"))
			{
				string versionsString = File.ReadAllText($"{rootPath}/versions.json");
				List<SortedDictionary<string, object>> versionsData = JsonConvert.DeserializeObject<List<SortedDictionary<string, object>>>(versionsString);
				if (versionsData.All(x => (string)x["assets"] != newVersion.title))
				{
					string branchName = Steamworks.SteamApps.CurrentBetaName ?? "public";
                    
					// Remove branch from latest list
					foreach (var entry in versionsData)
					{
						if (!entry.TryGetValue("latest", out object latestObj)) continue;
						if (latestObj is not JArray latestArray) continue;
                        
						List<string> latestList = latestArray.ToObject<List<string>>();
						latestList.RemoveAll(b => b == branchName);
						if (latestList.Count > 0) entry["latest"] = latestList;
						else entry.Remove("latest");
					}
                    
					string branchPassword = versionsData.FirstOrDefault(x => (string)x["branch"] == branchName)?.GetValueOrDefault("password", null) as string;
                    
					versionsData.Insert(0, new SortedDictionary<string, object>
					{
						{ "assets", newVersion.title },
						{ "branch", branchName },
						{ "date", DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:sszzz") },
						{ "latest", new List<string> { branchName } },
						{ "manifest", Steamworks.SteamApps.BuildId.ToString() },
						{ "password", branchPassword },
						{ "title", newVersion.title },
						{ "version", GetMajorMinorPatch(newVersion.title) },
					});
					File.WriteAllText($"{rootPath}/versions.json", JsonConvert.SerializeObject(versionsData, Formatting.Indented));
				}
                
				lastVersion = (string)versionsData.FirstOrDefault(x => (string)x["assets"] != newVersion.title)?["assets"];
			}
            
			GenerateDiffsJSON(exportPath);
			GenerateDiffsCode(exportPath, newVersion.title, lastVersion);
			
			checkedVersion = true;
		}

		static string GetMajorMinorPatch(string version)
		{
			if (version.StartsWith("v")) version = version.Substring(1);

			char[] separators = { '.', '_' };
			string[] parts = version.Split(separators, StringSplitOptions.RemoveEmptyEntries);

			if (parts.Length >= 3) return $"{parts[0]}.{parts[1]}.{parts[2]}";

			return version;
		}
        
		#region Code Diffs
		private static void GenerateDiffsCode(string exportPath, string newVersion, string oldVersion)
		{
			if (!File.Exists(batchFilePath))
			{
				Debug.LogWarning($"GenerateDiffsCode: Batch file not found at path: {batchFilePath}");
				return;
			}
            
			try
			{
				var psi = new ProcessStartInfo
				{
					FileName = batchFilePath,
					Arguments = $"\"{oldVersion}\" \"{newVersion}\" \"{exportPath}\"",
					UseShellExecute = false,
					RedirectStandardOutput = true,
					RedirectStandardError = true,
					CreateNoWindow = true,
					WorkingDirectory = Path.GetDirectoryName(batchFilePath)
				};
                
				using (var proc = new Process())
				{
					proc.StartInfo = psi;
					Debug.Log($"GenerateDiffsCode: Running batch: \"{psi.FileName}\" {psi.Arguments}");
					proc.Start();
                    
					string stdOut = proc.StandardOutput.ReadToEnd();
					string stdErr = proc.StandardError.ReadToEnd();
                    
					proc.WaitForExit();
                    
					if (!string.IsNullOrEmpty(stdOut)) Debug.Log($"[DiffsBatch][OUT]\n{stdOut}");
					if (!string.IsNullOrEmpty(stdErr)) Debug.LogWarning($"[DiffsBatch][ERR]\n{stdErr}");
					if (proc.ExitCode != 0) Debug.LogError($"GenerateDiffsCode: Batch exited with code {proc.ExitCode}.");
					else Debug.Log("GenerateDiffsCode: Diff generation batch completed successfully.");
				}
			}
			catch (Exception ex)
			{
				Debug.LogError($"GenerateDiffsCode: Exception while running batch: {ex}");
			}
		}
		#endregion

		#region JSON Diffs
		private static void GenerateDiffsJSON(string exportPath)
		{
			SortedDictionary<string, dynamic> generalData = new SortedDictionary<string, dynamic> {
				{ "itemSpawnTargetAmount", ShopManager.instance.itemSpawnTargetAmount },
				{ "itemConsumablesAmount", ShopManager.instance.itemConsumablesAmount },
				{ "itemUpgradesAmount", ShopManager.instance.itemUpgradesAmount },
				{ "itemHealthPacksAmount", ShopManager.instance.itemHealthPacksAmount },
				{ "itemValueMultiplier", ShopManager.instance.itemValueMultiplier },
				{ "upgradeValueIncrease", ShopManager.instance.upgradeValueIncrease },
				{ "healthPackValueIncrease", ShopManager.instance.healthPackValueIncrease },
				{ "crystalValueIncrease", ShopManager.instance.crystalValueIncrease },
			};
			if (!string.IsNullOrEmpty(exportPath))
			{
				string fileExportPath = $"{exportPath}\\general.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(generalData, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
			}

			SortedDictionary<string, dynamic> levelsData = new SortedDictionary<string, dynamic>();
			SortedDictionary<string, dynamic> valuablesData = new SortedDictionary<string, dynamic>();
			foreach (Level level in Resources.FindObjectsOfTypeAll<Level>()) {
				Dictionary<string, dynamic> levelResult = new Dictionary<string, dynamic>
				{
					{ "name", level.name },
					{ "label", level.NarrativeName },
					{ "ModuleAmount", level.ModuleAmount },
					{ "PassageMaxAmount", level.PassageMaxAmount },
					{ "HasEnemies", level.HasEnemies },
					{ "Modules", new Dictionary<string, dynamic>
						{
							{ "StartRooms", level.StartRooms.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Normal1", level.ModulesNormal1.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Normal2", level.ModulesNormal2.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Normal3", level.ModulesNormal3.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Passage1", level.ModulesPassage1.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Passage2", level.ModulesPassage2.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Passage3", level.ModulesPassage3.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "DeadEnd1", level.ModulesDeadEnd1.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "DeadEnd2", level.ModulesDeadEnd2.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "DeadEnd3", level.ModulesDeadEnd3.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Extraction1", level.ModulesExtraction1.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Extraction2", level.ModulesExtraction2.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "Extraction3", level.ModulesExtraction3.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						}
					},
					{ "ValuablePresets", new Dictionary<string, dynamic>
						{
							{ "tiny", level.ValuablePresets.SelectMany(p => p.tiny).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "small", level.ValuablePresets.SelectMany(p => p.small).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "medium", level.ValuablePresets.SelectMany(p => p.medium).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "big", level.ValuablePresets.SelectMany(p => p.big).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "wide", level.ValuablePresets.SelectMany(p => p.wide).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "tall", level.ValuablePresets.SelectMany(p => p.tall).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							{ "veryTall", level.ValuablePresets.SelectMany(p => p.veryTall).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						}
					},
					{ "AmbiencePresets", level.AmbiencePresets.Where(p => p?.breakers != null).SelectMany(p => p.breakers).GroupBy(x => x.sound.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
				};
				levelsData.Add(level.name, levelResult);

				if (level.HasEnemies)
				{
					List<ValuableObject> ValuablePresets = level.ValuablePresets
						.SelectMany(p => p.tiny
							.Concat(p.small)
							.Concat(p.medium)
							.Concat(p.big)
							.Concat(p.wide)
							.Concat(p.tall)
							.Concat(p.veryTall)
						)
						.Select(x => x.GetComponent<ValuableObject>())
						.Where(x => x)
						.ToList();
					foreach(ValuableObject item in ValuablePresets)
					{
						if (valuablesData.ContainsKey(item.name))
						{
							continue;
						}

						Dictionary<string, dynamic> valuablesResult = new Dictionary<string, dynamic>
						{
							{ "name", item.name },
							{ "label", item.name },
							{ "fragility", item.durabilityPreset.fragility },
							{ "durability", item.durabilityPreset.durability },
							{ "valueMin", item.valuePreset.valueMin },
							{ "valueMax", item.valuePreset.valueMax },
							{ "mass", item.physAttributePreset?.mass },
							{ "audioPreset", new Dictionary<string, dynamic> {
								{ "impactLight", item.audioPreset.impactLight.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "impactMedium", item.audioPreset.impactMedium.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "impactHeavy", item.audioPreset.impactHeavy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "breakLight", item.audioPreset.breakLight.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "breakMedium", item.audioPreset.breakMedium.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "breakHeavy", item.audioPreset.breakHeavy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
								{ "destroy", item.audioPreset.destroy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
							} },
							{ "volumeType", item.volumeType.ToString() },
							{ "ValuablePresets", true },
						};
						valuablesData.Add(item.name, valuablesResult);
					}
				}
			}
			if (!string.IsNullOrEmpty(exportPath))
			{
				string fileExportPath = $"{exportPath}\\levels.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(levelsData, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
			}

			SortedDictionary<string, dynamic> enemyData = new SortedDictionary<string, dynamic>();
			SortedDictionary<string, dynamic> enemySpawns = new SortedDictionary<string, dynamic>();
			foreach(EnemySetup enemySetup in Resources.FindObjectsOfTypeAll<EnemySetup>())
			{
				int spawnDifficulty = 0;
				if (EnemyDirector.instance.enemiesDifficulty3.Contains(enemySetup))
					spawnDifficulty = 3;
				else if (EnemyDirector.instance.enemiesDifficulty2.Contains(enemySetup))
					spawnDifficulty = 2;
				else if (EnemyDirector.instance.enemiesDifficulty1.Contains(enemySetup))
					spawnDifficulty = 1;

				if (spawnDifficulty == 0) continue;

				Dictionary<string, dynamic> enemyResult = new Dictionary<string, dynamic>
				{
					{ "name", enemySetup.name },
					{ "label", enemySetup.name },
					{ "difficulty", spawnDifficulty },
					{ "spawnObjects", enemySetup.spawnObjects.Where(x => !x.name.EndsWith(" Director")).GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
					{ "levelsCompletedCondition", enemySetup.levelsCompletedCondition },
					{ "levelsCompletedMin", enemySetup.levelsCompletedMin },
					{ "levelsCompletedMax", enemySetup.levelsCompletedMax },
					{ "rarity", enemySetup.rarityPreset?.chance },
					{ "runsPlayed", enemySetup.runsPlayed },
				};
				enemySpawns.Add(enemySetup.name, enemyResult);

				if (!enemySetup.name.StartsWith("Enemy Group - "))
				{
					EnemyParent enemyType = enemySetup.spawnObjects.FirstOrDefault(x => x.GetComponent<EnemyParent>()).GetComponent<EnemyParent>();
					if (enemyType)
					{
						Dictionary<string, dynamic> enemyResult1 = new Dictionary<string, dynamic>
						{
							{ "name", enemyType.name },
							{ "label", enemyType.enemyName },
							{ "difficulty", int.Parse(enemyType.difficulty.ToString().Replace("Difficulty", "")) },
							{ "overchargeMultiplier", enemyType.overchargeMultiplier },
							{ "SpawnedTimeMin", enemyType.SpawnedTimeMin },
							{ "SpawnedTimeMax", enemyType.SpawnedTimeMax },
							{ "DespawnedTimeMin", enemyType.DespawnedTimeMin },
							{ "DespawnedTimeMax", enemyType.DespawnedTimeMax },
						};
						enemyData.Add(enemyType.enemyName, enemyResult1);
					}
				}
			}
			if (!string.IsNullOrEmpty(exportPath))
			{
				string fileExportPath = $"{exportPath}\\enemies.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(enemyData, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
				fileExportPath = $"{exportPath}\\enemy_spawns.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(enemySpawns, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
			}

			SortedDictionary<string, dynamic> itemsData = new SortedDictionary<string, dynamic>();
			foreach(Item item in Resources.FindObjectsOfTypeAll<Item>())
			{
				Dictionary<string, dynamic> itemsResult = new Dictionary<string, dynamic>
				{
					{ "name", item.name },
					{ "label", item.itemName },
					{ "itemType", item.itemType.ToString() },
					{ "itemVolume", item.itemVolume.ToString() },
					{ "itemSecretShopType", item.itemSecretShopType.ToString() },
					{ "valueMin", item.value.valueMin },
					{ "valueMax", item.value.valueMax },
					{ "maxAmount", item.maxAmount },
					{ "maxAmountInShop", item.maxAmountInShop },
					{ "maxPurchase", item.maxPurchase },
					{ "maxPurchaseAmount", item.maxPurchaseAmount },
					{ "itemDictionary", StatsManager.instance.itemDictionary.Values.Any(x => x.name == item.name) },
				};
				itemsData.Add(item.name, itemsResult);
			}
			if (!string.IsNullOrEmpty(exportPath))
			{
				string fileExportPath = $"{exportPath}\\items.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(itemsData, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
			}

			foreach(ValuableObject item in Resources.FindObjectsOfTypeAll<ValuableObject>())
			{
				if (valuablesData.ContainsKey(item.name) || (!item.name.StartsWith("Enemy Valuable") && !item.name.StartsWith("Surplus Valuable")))
				{
					continue;
				}

				Dictionary<string, dynamic> itemsResult = new Dictionary<string, dynamic>
				{
					{ "name", item.name },
					{ "label", item.name },
					{ "fragility", item.durabilityPreset.fragility },
					{ "durability", item.durabilityPreset.durability },
					{ "valueMin", item.valuePreset.valueMin },
					{ "valueMax", item.valuePreset.valueMax },
					{ "mass", item.physAttributePreset?.mass },
					{ "audioPreset", new Dictionary<string, dynamic> {
						{ "impactLight", item.audioPreset.impactLight.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "impactMedium", item.audioPreset.impactMedium.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "impactHeavy", item.audioPreset.impactHeavy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "breakLight", item.audioPreset.breakLight.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "breakMedium", item.audioPreset.breakMedium.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "breakHeavy", item.audioPreset.breakHeavy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
						{ "destroy", item.audioPreset.destroy.Sounds.GroupBy(x => x.name).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Count()) },
					} },
					{ "volumeType", item.volumeType.ToString() },
					{ "ValuablePresets", false },
				};
				valuablesData.Add(item.name, itemsResult);
			}
			if (!string.IsNullOrEmpty(exportPath))
			{
				string fileExportPath = $"{exportPath}\\valuables.json";
				if (!File.Exists(fileExportPath))
				{
					File.WriteAllText(fileExportPath, JsonConvert.SerializeObject(valuablesData, Formatting.Indented, new JsonSerializerSettings {
						ReferenceLoopHandling = ReferenceLoopHandling.Ignore
					}));
				}
				else
				{
					Debug.LogWarning($"{Path.GetFileName(fileExportPath)} already exists, skipping export.");
				}
			}
		}
		#endregion
	}
}