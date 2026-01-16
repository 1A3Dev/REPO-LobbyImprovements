using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LobbyImprovements;

public class ObjectScreenshotTaker : MonoBehaviour
{
	private readonly string SavePath = "Screenshots";
	private readonly string ImageMapFileName = "images.json";
	
	internal static ObjectScreenshotTaker instance;
	internal bool isTakingScreenshots;
	
	private Camera ScreenshotCamera;
	private Light ScreenshotLight;
	private GameObject moduleObject;
	private RenderTexture renderTexture;
	private Texture2D screenshot;
	private Color? ambientLight;

	private enum ScreenshotTypes
	{
		None,
		Modules,
		Enemies,
		Items,
		Valuables
	}
	private ScreenshotTypes screenshotType = ScreenshotTypes.None;

	private Dictionary<string, Dictionary<string, string>> nameGuids = new();
	
	private void Awake()
	{
		if (instance)
		{
			Destroy(gameObject);
			return;
		}
		instance = this;
		
		if (ScreenshotCamera == null)
		{
			ScreenshotCamera = gameObject.AddComponent<Camera>();
			ScreenshotCamera.CopyFrom(Camera.main);
			ScreenshotCamera.name = "ScreenshotCamera";
			ScreenshotCamera.cullingMask = -4129;
			ScreenshotCamera.farClipPlane = 2000f;
			ScreenshotCamera.enabled = false;
			// Transparency
			ScreenshotCamera.clearFlags = CameraClearFlags.SolidColor;
			ScreenshotCamera.backgroundColor = new Color(0, 0, 0, 0);
		}
		
		if (ScreenshotLight == null)
		{
			Light sourceLight = PlayerAvatar.instance.flashlightController.spotlight;
			GameObject lightObj = new GameObject("ScreenshotLight");
			ScreenshotLight = lightObj.AddComponent<Light>();
			ScreenshotLight.type = sourceLight.type;
			ScreenshotLight.color = sourceLight.color;
			ScreenshotLight.intensity = sourceLight.intensity;
			ScreenshotLight.range = 100f;
			ScreenshotLight.spotAngle = 100f;
			ScreenshotLight.shadows = sourceLight.shadows;
			ScreenshotLight.cookie = sourceLight.cookie;
			ScreenshotLight.cookieSize = sourceLight.cookieSize;
			ScreenshotLight.renderMode = sourceLight.renderMode;
			ScreenshotLight.enabled = false;
		}
		
		if (!Directory.Exists(SavePath))
			Directory.CreateDirectory(SavePath);
		
		string jsonPath = Path.Combine(SavePath, ImageMapFileName);
		if(File.Exists(jsonPath)){
			string json = File.ReadAllText(jsonPath);
			nameGuids = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, string>>>(json) ?? new Dictionary<string, Dictionary<string, string>>();
		}
		
		// Disable level objects
		GameObject startRoom = LevelGenerator.Instance.LevelParent.GetComponentInChildren<StartRoom>().gameObject;
		foreach (PhysGrabObject physGrabObject in Object.FindObjectsOfType<PhysGrabObject>())
		{
			physGrabObject.DestroyPhysGrabObject();
		}
		foreach (Light light in startRoom.transform.GetComponentsInChildren<Light>())
		{
			light.enabled = false;
		}
		foreach (Renderer renderer in startRoom.transform.GetComponentsInChildren<Renderer>())
		{
			renderer.enabled = false;
		}
		foreach (Canvas canvas in startRoom.transform.Find("Truck/Truck Run").GetComponentsInChildren<Canvas>())
		{
			canvas.enabled = false;
		}
		
		foreach (Light light in PlayerAvatar.instance.GetComponentsInChildren<Light>())
		{
			light.enabled = false;
		}
		foreach (Renderer renderer in PlayerAvatar.instance.GetComponentsInChildren<Renderer>())
		{
			renderer.enabled = false;
		}
	}

	private void OnDestroy()
	{
		PluginLoader.StaticLogger.LogInfo("[ModuleScreenshotTaker] Destroying...");
		if (ScreenshotLight) Destroy(ScreenshotLight.gameObject);
	}

	private void ScreenshotStart()
	{
		isTakingScreenshots = true;
		PlayerAvatar.instance.flashlightController.hideFlashlight = true;
		PluginLoader.StaticLogger.LogInfo("[ModuleScreenshotTaker] Starting");
	}
	
	private void ScreenshotEnd()
	{
		if (ScreenshotLight) ScreenshotLight.enabled = false;
		PlayerAvatar.instance.flashlightController.hideFlashlight = false;
		ambientLight = null;
		EnvironmentDirector.Instance.DarkAdaptationLerp = !FlashlightController.Instance.LightActive ? 0.01f : 0.99f;
		isTakingScreenshots = false;
		PluginLoader.StaticLogger.LogInfo("[ModuleScreenshotTaker] Finished");
		// RunManager.instance.ChangeLevel(false, false, RunManager.ChangeLevelType.Recording);
		if (ScreenshotLight) Destroy(ScreenshotLight.gameObject);
		Destroy(gameObject);
	}
	
	internal IEnumerator TakeScreenshotsOfEnemies()
	{
		screenshotType = ScreenshotTypes.Enemies;
		ScreenshotStart();
		yield return new WaitForSeconds(2f);
		
		List<GameObject> enemies = EnemyDirector.instance.enemiesDifficulty1
			.Concat(EnemyDirector.instance.enemiesDifficulty2)
			.Concat(EnemyDirector.instance.enemiesDifficulty3)
			.Where(x => x && !x.name.Contains("Enemy Group - "))
			.Select(x => x.spawnObjects.FirstOrDefault(o => !o.PrefabName.Contains("Director"))?.Prefab)
			.Where(x => x)
			.ToList();
		
		yield return StartCoroutine(TakeScreenshotsCoroutine(enemies.Distinct().OrderBy(x => x.name).ToArray(), "Enemies", "Enemies"));
		
		ScreenshotEnd();
	}
	
	internal IEnumerator TakeScreenshotsOfItems()
	{
		screenshotType = ScreenshotTypes.Items;
		ScreenshotStart();
		yield return new WaitForSeconds(2f);
		
		List<Item> items = Resources.FindObjectsOfTypeAll<Item>().Where(x => x && x.prefab.Prefab).ToList();
		
		yield return StartCoroutine(TakeScreenshotsCoroutine(items.OrderBy(x => x.name).Select(x => x.prefab.Prefab).Distinct().ToArray(), "Items", "Items"));

		ScreenshotEnd();
	}
	
	internal IEnumerator TakeScreenshotsOfModules()
	{
		screenshotType = ScreenshotTypes.Modules;
		ScreenshotStart();
		yield return new WaitForSeconds(2f);
		
		// List<Level> levels = RunManager.instance.levels;
		List<Level> levels = Resources.FindObjectsOfTypeAll<Level>().Where(x => x &&
			x.name != "Level - Lobby" &&
			x.name != "Level - Lobby Menu" &&
			x.name != "Level - Main Menu" &&
			x.name != "Level - Splash Screen"
		).ToList();
		
		yield return StartCoroutine(TakeScreenshotsCoroutine(levels.SelectMany(x => x.StartRooms).Select(x => x.Prefab).Distinct().OrderBy(x => x.name).ToArray(), "StartRoom", "Modules"));
		yield return StartCoroutine(TakeScreenshotsCoroutine(levels.SelectMany(x => x.ModulesNormal1.Concat(x.ModulesNormal2).Concat(x.ModulesNormal3)).Select(x => x.Prefab).Distinct().OrderBy(x => x.name).ToArray(), "Normal", "Modules"));
		yield return StartCoroutine(TakeScreenshotsCoroutine(levels.SelectMany(x => x.ModulesPassage1.Concat(x.ModulesPassage2).Concat(x.ModulesPassage3)).Select(x => x.Prefab).Distinct().OrderBy(x => x.name).ToArray(), "Passage", "Modules"));
		yield return StartCoroutine(TakeScreenshotsCoroutine(levels.SelectMany(x => x.ModulesDeadEnd1.Concat(x.ModulesDeadEnd2).Concat(x.ModulesDeadEnd3)).Select(x => x.Prefab).Distinct().OrderBy(x => x.name).ToArray(), "DeadEnd", "Modules"));
		yield return StartCoroutine(TakeScreenshotsCoroutine(levels.SelectMany(x => x.ModulesExtraction1.Concat(x.ModulesExtraction2).Concat(x.ModulesExtraction3)).Select(x => x.Prefab).Distinct().OrderBy(x => x.name).ToArray(), "Extraction", "Modules"));
		
		ScreenshotEnd();
	}
	
	internal IEnumerator TakeScreenshotsOfValuables()
	{
		screenshotType = ScreenshotTypes.Valuables;
		ScreenshotStart();
		yield return new WaitForSeconds(2f);
		
		List<ValuableObject> valuables = RunManager.instance.levels
			.SelectMany(x => x.ValuablePresets.SelectMany(p => p.tiny.Concat(p.small).Concat(p.medium).Concat(p.big).Concat(p.wide).Concat(p.tall).Concat(p.veryTall)))
			.Select(x => x.Prefab.GetComponent<ValuableObject>())
			.Concat(Resources.FindObjectsOfTypeAll<ValuableObject>().Where(item => item.name.StartsWith("Enemy Valuable - ") || item.name.StartsWith("Surplus Valuable - ")))
			.Where(x => x && x.gameObject)
			.ToList();
		
		yield return StartCoroutine(TakeScreenshotsCoroutine(valuables.OrderBy(x => x.name).Select(x => x.gameObject).Distinct().ToArray(), "Valuables", "Valuables"));
		
		ScreenshotEnd();
	}
	
	private Bounds CalculateBounds(GameObject go, bool useColliders = false)
	{
		if (useColliders)
		{
			// var colliders = go.GetComponentsInChildren<Collider>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
			var colliders = go.GetComponentsInChildren<Collider>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy && !r.isTrigger).ToArray();
			if (colliders.Length > 0)
			{
				Bounds bounds = colliders[0].bounds;
				for (int i = 1; i < colliders.Length; i++)
				{
					bounds.Encapsulate(colliders[i].bounds);
				}
				return bounds;
			}
		}

		var renderers = go.GetComponentsInChildren<Renderer>(true).Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
		if (renderers.Length > 0)
		{
			Bounds bounds = renderers[0].bounds;
			for (int i = 1; i < renderers.Length; i++)
			{
				bounds.Encapsulate(renderers[i].bounds);
			}

			return bounds;
		}

		PluginLoader.StaticLogger.LogWarning($"No renderers found for {go.name}");
		return new Bounds(go.transform.position, Vector3.one);
	}

	private IEnumerator TakeScreenshotsCoroutine(GameObject[] modules, string category, string ssType)
	{
		if (!Directory.Exists($"{SavePath}/{ssType}"))
			Directory.CreateDirectory($"{SavePath}/{ssType}");

		bool updatedNameGuids = false;
		
		foreach (var module in modules)
		{
			if (!module || (ssType == "Modules" && module.name.StartsWith("Start Room - Shop"))) continue;

			string fileNameRaw = module.name;
			if (ssType == "Enemies")
				fileNameRaw = module.name.Replace("Enemy - ", "");
			else if (ssType == "Modules")
				fileNameRaw = Regex.Replace(module.name, " - [0-9]+ - ", " - ");
			
			string fileName = $"{SavePath}/{ssType}/{fileNameRaw}.png";
			if (File.Exists(fileName)) continue;
			
			// Randomly generated file names for tester builds
			if(SteamApps.CurrentBetaName == "tester"){
				if(!nameGuids.ContainsKey(ssType)) nameGuids[ssType] = new();
				if(nameGuids[ssType].TryGetValue(fileNameRaw, out var guid)){
					if(File.Exists(guid)) continue;
					fileName = $"{SavePath}/{ssType}/{guid}.png";
				}else{
					string newGuid = System.Guid.NewGuid().ToString();
					fileName = $"{SavePath}/{ssType}/{newGuid}.png";
					while(File.Exists(fileName)){
						newGuid = System.Guid.NewGuid().ToString();
						fileName = $"{SavePath}/{ssType}/{newGuid}.png";
					}
					nameGuids[ssType][fileNameRaw] = newGuid;
					updatedNameGuids = true;
				}
			}
			
			// Required logic before spawning
			if (ssType == "Valuables")
			{
				if (module.name.StartsWith("Surplus Valuable - "))
				{
					Time.timeScale = 0f;
				}
			}
			
			moduleObject = Instantiate(module, Vector3.zero, Quaternion.identity);
			yield return null;
			
			bool useCollisionsForBounds = false;
			GameObject boundsObject = moduleObject;
			if (ssType == "Enemies")
			{
				Enemy enemy = moduleObject.GetComponentInChildren<Enemy>();
				enemy?.Freeze(7f);
				
				if (module.name == "Enemy - Bomb Thrower")
				{
					boundsObject = enemy.Rigidbody.gameObject;
				}
				else if (module.name == "Enemy - Elsa")
				{
					boundsObject = enemy.gameObject.GetComponent<EnemyElsa>().visualsTransform.gameObject;
				}
				else if (module.name == "Enemy - Oogly")
				{
					boundsObject = enemy.gameObject.GetComponent<EnemyOogly>().visualTransform.gameObject;
				}
				
				if (module.name != "Enemy - Beamer" && module.name != "Enemy - Shadow" && module.name != "Enemy - Slow Walker" && module.name != "Enemy - Upscream")
				{
					useCollisionsForBounds = true;
				}
				
				// Allow time for the spawn animations to finish
				if (module.name == "Enemy - Bomb Thrower" || module.name == "Enemy - Floater" || module.name == "Enemy - Runner" || module.name == "Enemy - Slow Walker" || module.name == "Enemy - Tumbler")
				{
					yield return new WaitForSeconds(1f);
				}
				else if (module.name == "Enemy - Hidden" || module.name == "Enemy - Hunter" || module.name == "Enemy - Oogly")
				{
					yield return new WaitForSeconds(5f);
				}
			}
			else if (ssType == "Items")
			{
				Rigidbody rb = moduleObject.GetComponent<Rigidbody>();
				if (rb)
				{
					rb.useGravity = false;
					rb.velocity = Vector3.zero;
					rb.constraints = RigidbodyConstraints.FreezeAll;
				}
				
				PhysGrabObjectImpactDetector impactDetector = moduleObject.GetComponent<PhysGrabObjectImpactDetector>();
				if (impactDetector && !impactDetector.particleDisable)
				{
					impactDetector.particleDisable = true;
				}
				
				if (module.name == "Item Cart Cannon" || module.name == "Item Cart Laser")
				{
					// Hide the battery visual
					moduleObject.GetComponent<ItemCartCannonMain>()?.battery?.batteryVisualLogic?.gameObject.SetActive(false);
				}

				useCollisionsForBounds = true;
				
				if (module.name == "Item Melee Baseball Bat" || module.name == "Item Melee Frying Pan" || module.name.StartsWith("Item WalkieTalkie"))
				{
					yield return new WaitForSeconds(1f);
				}
			}
			else if (ssType == "Modules")
			{
				foreach (PhysGrabObject physGrabObject in Object.FindObjectsOfType<PhysGrabObject>())
				{
					if (!physGrabObject.hasHinge)
					{
						physGrabObject.DestroyPhysGrabObject();
					}
				}
			}
			else if (ssType == "Valuables")
			{
				Rigidbody rb = moduleObject.GetComponent<Rigidbody>();
				if (rb)
				{
					rb.useGravity = false;
					rb.velocity = Vector3.zero;
					rb.constraints = RigidbodyConstraints.FreezeAll;
				}
				
				PhysGrabObjectImpactDetector impactDetector = moduleObject.GetComponent<PhysGrabObjectImpactDetector>();
				if (impactDetector && !impactDetector.particleDisable)
				{
					impactDetector.particleDisable = true;
				}

				if (module.name == "Valuable Gumball")
				{
					useCollisionsForBounds = true;
				}
			}

			Bounds bounds = CalculateBounds(boundsObject, useCollisionsForBounds);
			float maxSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
			ScreenshotCamera.orthographic = !module.name.StartsWith("Enemy Valuable - ");
			ScreenshotCamera.orthographicSize = maxSize * 0.8f;
			Vector3 isoDirection = new Vector3(1, 1, -1).normalized;
			if (ssType == "Enemies")
			{
				isoDirection = Vector3.forward;
			}
			else if (ssType == "Items")
			{
				if(module.name.StartsWith("Item Upgrade ") || module.name.StartsWith("Item WalkieTalkie")){
					isoDirection = new Vector3(1, 1, 1).normalized;
				}
			}
			else if (ssType == "Valuables")
			{
				isoDirection = new Vector3(1, 1, 1).normalized;
			}
			float padding = 1f;
			if (ssType == "Items")
			{
				padding = 0.75f;
			}
			float distance = maxSize * padding;
			ScreenshotCamera.transform.position = bounds.center + isoDirection * distance;
			ScreenshotCamera.transform.LookAt(bounds.center);
			
			// === Light ===
			if (ScreenshotLight)
			{
				ScreenshotLight.transform.position = bounds.center + isoDirection * distance;
				ScreenshotLight.transform.LookAt(bounds.center);
			}
			
			if (ssType != "Modules")
			{
				if (ScreenshotLight)
				{
					if (ssType == "Valuables" && (
					    module.name == "Valuable Goblet" ||
					    module.name == "Valuable GoldFish" ||
					    module.name == "Valuable GoldTooth" ||
					    module.name == "Valuable Piano" ||
					    module.name == "Valuable SilverFish" ||
					    module.name == "Valuable Trophy" ||
					    module.name == "Valuable Wizard Griffin Statue" ||
					    module.name == "Valuable Wizard Master Potion" ||
					    module.name == "Valuable Wizard Sword"
					))
					{
						ScreenshotLight.enabled = true;
					}
					else
					{
						ScreenshotLight.enabled = false;
					}
				}
				
				if ((ssType == "Enemies" && module.name == "Enemy - Gnome") || (ssType == "Items" && module.name == "Item Duck Bucket"))
				{
					ambientLight = new Color(0.5f, 0.5f, 0.5f);
				}
				else
				{
					ambientLight = new Color(1f, 1f, 1f);
				}
				RenderSettings.ambientLight = ambientLight.Value;
			}

			yield return new WaitForEndOfFrame();
			TakeScreenshot(fileName);
			PluginLoader.StaticLogger.LogDebug($"Screenshot saved: {fileNameRaw} ({Path.GetFileName(fileName)})");
			Destroy(moduleObject);
			
			if (Time.timeScale == 0f) Time.timeScale = 1f;
		}

		if(updatedNameGuids){
			string outputJson = JsonConvert.SerializeObject(nameGuids, Formatting.Indented);
			File.WriteAllText(Path.Combine(SavePath, ImageMapFileName), outputJson);
		}
	}

	private void TakeScreenshot(string filePath)
	{
		renderTexture = new RenderTexture(1920, 1920, 32, RenderTextureFormat.ARGB32);
		ScreenshotCamera.targetTexture = renderTexture;
		ScreenshotCamera.Render();

		RenderTexture.active = renderTexture;
		screenshot = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
		screenshot.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
		screenshot.Apply();
		
		byte[] bytes = screenshot.EncodeToPNG();
		File.WriteAllBytes(filePath, bytes);

		ScreenshotCamera.targetTexture = null;
		RenderTexture.active = null;
		Destroy(renderTexture);
		Destroy(screenshot);
	}

	private void LateUpdate()
	{
		if (!isTakingScreenshots) return;
		
		if (ambientLight.HasValue)
		{
			RenderSettings.ambientLight = ambientLight.Value;
		}
	}
}
