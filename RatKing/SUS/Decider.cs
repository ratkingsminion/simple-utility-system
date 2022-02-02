#if UNITY_EDITOR
#define ALLOW_IMGUI_DEBUG
#endif

using System.Collections.Generic;
using UnityEngine;

namespace RatKing.SUS {

	public enum DebugDisplayMode {
		None,
		Full,
		Minimized
	}

#if ALLOW_IMGUI_DEBUG
	public class DeciderDebugDraw : MonoBehaviour {
		public class DebugDraw { public object target; public System.Func<object, float, DebugDraw, float> draw; public DebugDisplayMode display; public Vector2 scrollPosDebug; }

		public static DeciderDebugDraw instance;
		public static DeciderDebugDraw Inst => instance != null ? instance : (instance = new GameObject("<DECIDER_DEBUG>").AddComponent<DeciderDebugDraw>());
		List<DebugDraw> debugDraws = new List<DebugDraw>();
		public void AddDebugDraw(object target, System.Func<object, float, DebugDraw, float> debugUI, DebugDisplayMode mode) {
			debugDraws.Add(new DebugDraw() { target = target, draw = debugUI, display = mode });
		}
		void OnGUI() {
			float drawX = 10f;
			for (int i = 0, count = debugDraws.Count; i < count; ++i) {
				var dd = debugDraws[i];
				if (dd.display == DebugDisplayMode.None || dd.target == null || dd.target.Equals(null)) { debugDraws.RemoveAt(i); --i; --count; continue; }
				drawX += dd.draw.Invoke(dd.target, drawX, debugDraws[i]);
			}
			if (debugDraws.Count == 0) {
				Destroy(gameObject);
			}
		}
	}
#endif

	public class Decider {

		public Action ActiveAction { get; private set; } = null;
		public Action ConsideredAction { get; private set; } = null; // only valid during consideration!
		public Action ThisAction { get; private set; } = null; // only valid during consideration and execution of active action!
		public float ActiveActionAge => Time.time - actionChangeTime;
		public bool IsConsideringActiveAction => ActiveAction == ConsideredAction;
		public event System.Action<Action, Action> OnActionChange = null; // target, prevAction, nextAction
		List<Action> actions = new List<Action>();
		float actionChangeTime = 0f;

		//

#if ALLOW_IMGUI_DEBUG
		public Decider(object target, DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { DeciderDebugDraw.Inst.AddDebugDraw(target, DebugGUI, debugDisplayMode); }
			this.actionChangeTime = Time.time;
		}
#else
		public Decider(DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { Debug.Log("Debug display for Decider not allowed."); }
			this.actionChangeTime = Time.time;
		}
#endif

		public void Update(float dt) {
			if (actions.Count == 0) { return; }

			// calculate scores
			foreach (var pa in actions) {
				if (!pa.getsConsidered) { continue; }
				ThisAction = ConsideredAction = pa;
				pa.Calculate(dt);
			}
			ThisAction = ConsideredAction = null;

			// choose action
			Action chosenAction = null;
			var score = float.NegativeInfinity;
			foreach (var pa in actions) {
				if (pa.lastCalculatedScore > score) {
					chosenAction = pa;
					score = pa.lastCalculatedScore;
				}
			}

			// switch current action
			ThisAction = ActiveAction;
			if (ActiveAction != chosenAction) {
				actionChangeTime = Time.time;
				var prevAction = ActiveAction;
				prevAction?.onStop?.Invoke();
				ThisAction = ActiveAction = chosenAction;
				OnActionChange?.Invoke(prevAction, ActiveAction);
				chosenAction.onStart?.Invoke();
			}
			if (ActiveAction != null) {
				ActiveAction.onUpdate?.Invoke();
			}
			ThisAction = null;
		}

#if ALLOW_IMGUI_DEBUG
		float DebugGUI(object target, float drawX, DeciderDebugDraw.DebugDraw dd) {
			var width = dd.display == DebugDisplayMode.Minimized ? 46f : 180f;
			var styleLine = new GUIStyle("label") { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = false };
			GUILayout.BeginArea(new Rect(drawX, 10, width, 205), "", "box");
			dd.scrollPosDebug = GUILayout.BeginScrollView(dd.scrollPosDebug); // new Rect(10f, 10f, 200f, 300f), GUIContent.none, style

			GUILayout.BeginHorizontal();
			GUI.color = dd.display == DebugDisplayMode.Minimized ? Color.green : Color.blue;
			if (GUILayout.Button("", GUILayout.Width(15f))) { dd.display = dd.display == DebugDisplayMode.Full ? DebugDisplayMode.Minimized : DebugDisplayMode.Full; }
			GUI.color = Color.red;
			if (GUILayout.Button("", GUILayout.Width(15f))) { dd.display = DebugDisplayMode.None; }
			GUI.color = Color.white;
			if (target is Object uo) { GUILayout.Label($"<b>{uo.name}</b> ({uo.GetType().Name})", styleLine); }
			else { GUILayout.Label($"<b>{target}</b>", styleLine); }
			GUILayout.EndHorizontal();
			if ((target is GameObject go && !go.activeInHierarchy) || (target is MonoBehaviour mb && (!mb.enabled || !mb.gameObject.activeInHierarchy))) {
				GUILayout.Label("<i>is disabled</i>");
			}

			var text = "";
			foreach (var a in actions) {
				if (text != "") { text += "\n"; }
				if (a == ActiveAction) { text += "<color=yellow>"; }
				text += a.lastCalculatedScore.ToString("0.00");
				if (!string.IsNullOrWhiteSpace(a.id)) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime().ToString("0.00")})"; }
				text += $" [{a.scoreCalculationStandardMethod.ToShortString()}]";
				for (int i = 0; i < a.considerations.Length; ++i) {
					text += $"\n   ({i}) {a.considerations[i]}";
				}
				if (a == ActiveAction) { text += "</color>"; }
			}

			GUILayout.Label(text, styleLine);
			GUILayout.EndScrollView();
			GUILayout.EndArea();
			return width + 5f;
		}
#endif

		//

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <returns>the new Consideration</returns>
		public Consideration Consider(string id, System.Func<float> function) {
			return new Consideration(id, function);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the new Consideration</returns>
		public Consideration Consider(System.Func<float> function) {
			return new Consideration(function);
		}

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration Consider(string id, System.Func<float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration(id, function, calculationMethod);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration Consider(System.Func<float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration(function, calculationMethod);
		}

		//

		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="onStop">called when this action becomes un-chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action AddAction(string id, System.Action onStart, System.Action onUpdate, System.Action onStop, params Consideration[] considerations) {
			var pa = new Action(id, onStart, onUpdate, onStop) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action AddAction(string id, System.Action onStart, System.Action onUpdate, params Consideration[] considerations) {
			var pa = new Action(id, onStart, onUpdate) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action AddAction(string id, System.Action onStart, params Consideration[] considerations) {
			var pa = new Action(id, onStart) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action AddAction(string id, params Consideration[] considerations) {
			var pa = new Action(id) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}

		// saving/loading

		public bool SerializeActiveAction(SimpleJSON.JSONNode node) {
			if (ActiveAction == null) { return false; }
			node.Add("action_id", ActiveAction.id);
			node.Add("action_age", ActiveActionAge);
			foreach (var a in actions) {
				node.Add($"action_{a.id}_score", a.lastCalculatedScore);
				foreach (var c in a.considerations) {
					node.Add($"action_{a.id}_consider_{c.id}", c.lastScore);
				}
			}
			return true;
		}

		public bool DeserializeActiveAction(SimpleJSON.JSONNode node) {
			if (!node.HasKey("action_id")) { return false; }
			foreach (var a in actions) {
				a.lastCalculatedScore = node[$"action_{a.id}_score"].AsFloat;
				foreach (var c in a.considerations) {
					c.lastScore = node[$"action_{a.id}_consider_{c.id}"];
				}
			}
			var id = node["action_id"].Value;
			ThisAction = ActiveAction = actions.Find(a => a.id == id);
			actionChangeTime = Time.time - node["action_age"].AsFloat;
			ActiveAction.onStart?.Invoke();
			OnActionChange?.Invoke(null, ActiveAction);
			ThisAction = null;
			return true;
		}
	}

	public class Decider<T> where T : class {
		T target;
		public T Target => target;
		public Action<T> ActiveAction { get; private set; } = null;
		public Action<T> ConsideredAction { get; private set; } = null; // only valid during consideration!
		public Action<T> ThisAction { get; private set; } = null; // only valid during consideration and execution of active action!
		public float ActiveActionAge => Time.time - actionChangeTime;
		public bool IsConsideringActiveAction => ActiveAction == ConsideredAction;
		public event System.Action<T, Action<T>, Action<T>> OnActionChange = null; // target, prevAction, nextAction
		List<Action<T>> actions = new List<Action<T>>();
		float actionChangeTime = 0f;

		//

#if ALLOW_IMGUI_DEBUG
		public Decider(T target, DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { DeciderDebugDraw.Inst.AddDebugDraw(target, DebugGUI, debugDisplayMode); }
			this.target = target;
			this.actionChangeTime = Time.time;
		}
#else
		public Decider(T target, DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { Debug.Log("Debug display for Decider not allowed."); }
			this.target = target;
			this.actionChangeTime = Time.time;
		}
#endif

		public void Update(float dt) {
			if (actions.Count == 0) { return; }

			// calculate scores
			foreach (var pa in actions) {
				if (!pa.getsConsidered) { continue; }
				ThisAction = ConsideredAction = pa;
				pa.Calculate(ref target, dt);
			}
			ThisAction = ConsideredAction = null;

			// choose action
			Action<T> chosenAction = null;
			var score = float.NegativeInfinity;
			foreach (var pa in actions) {
				if (pa.lastCalculatedScore > score) {
					chosenAction = pa;
					score = pa.lastCalculatedScore;
				}
			}

			// switch current action
			ThisAction = ActiveAction;
			if (ActiveAction != chosenAction) {
				actionChangeTime = Time.time;
				var prevAction = ActiveAction;
				prevAction?.onStop?.Invoke(target);
				ThisAction = ActiveAction = chosenAction;
				OnActionChange?.Invoke(target, prevAction, ActiveAction);
				chosenAction.onStart?.Invoke(target);
			}
			if (ActiveAction != null) {
				ActiveAction.onUpdate?.Invoke(target);
			}
			ThisAction = null;
		}

#if ALLOW_IMGUI_DEBUG
		float DebugGUI(object target, float drawX, DeciderDebugDraw.DebugDraw dd) {
			var width = dd.display == DebugDisplayMode.Minimized ? 46f : 180f;
			var styleLine = new GUIStyle("label") { richText = true, alignment = TextAnchor.UpperLeft, wordWrap = false };
			GUILayout.BeginArea(new Rect(drawX, 10, width, 205), "", "box");
			dd.scrollPosDebug = GUILayout.BeginScrollView(dd.scrollPosDebug); // new Rect(10f, 10f, 200f, 300f), GUIContent.none, style

			GUILayout.BeginHorizontal();
			GUI.color = dd.display == DebugDisplayMode.Minimized ? Color.green : Color.blue;
			if (GUILayout.Button("", GUILayout.Width(15f))) { dd.display = dd.display == DebugDisplayMode.Full ? DebugDisplayMode.Minimized : DebugDisplayMode.Full; }
			GUI.color = Color.red;
			if (GUILayout.Button("", GUILayout.Width(15f))) { dd.display = DebugDisplayMode.None; }
			GUI.color = Color.white;
			if (target is Object uo) { GUILayout.Label($"<b>{uo.name}</b> ({uo.GetType().Name})", styleLine); }
			else { GUILayout.Label($"<b>{target}</b>", styleLine); }
			GUILayout.EndHorizontal();
			if ((target is GameObject go && !go.activeInHierarchy) || (target is MonoBehaviour mb && (!mb.enabled || !mb.gameObject.activeInHierarchy))) {
				GUILayout.Label("<i>is disabled</i>");
			}

			var text = "";
			foreach (var a in actions) {
				if (text != "") { text += "\n"; }
				if (a == ActiveAction) { text += "<color=yellow>"; }
				text += a.lastCalculatedScore.ToString("0.00");
				if (!string.IsNullOrWhiteSpace(a.id)) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime().ToString("0.00")})"; }
				text += $" [{a.scoreCalculationStandardMethod.ToShortString()}]";
				for (int i = 0; i < a.considerations.Length; ++i) {
					text += $"\n   ({i}) {a.considerations[i]}";
				}
				if (a == ActiveAction) { text += "</color>"; }
			}

			GUILayout.Label(text, styleLine);
			GUILayout.EndScrollView();
			GUILayout.EndArea();
			return width + 5f;
		}
#endif

		//

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <returns>the new Consideration</returns>
		public Consideration<T> Consider(string id, System.Func<T, float> function) {
			return new Consideration<T>(id, function);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the new Consideration</returns>
		public Consideration<T> Consider(System.Func<T, float> function) {
			return new Consideration<T>(function);
		}

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration<T> Consider(string id, System.Func<T, float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration<T>(id, function, calculationMethod);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration<T> Consider(System.Func<T, float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration<T>(function, calculationMethod);
		}

		//

		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="onStop">called when this action becomes un-chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<T> AddAction(string id, System.Action<T> onStart, System.Action<T> onUpdate, System.Action<T> onStop, params Consideration<T>[] considerations) {
			var pa = new Action<T>(id, onStart, onUpdate, onStop) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<T> AddAction(string id, System.Action<T> onStart, System.Action<T> onUpdate, params Consideration<T>[] considerations) {
			var pa = new Action<T>(id, onStart, onUpdate) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<T> AddAction(string id, System.Action<T> onStart, params Consideration<T>[] considerations) {
			var pa = new Action<T>(id, onStart) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<T> AddAction(string id, params Consideration<T>[] considerations) {
			var pa = new Action<T>(id) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}

		// saving/loading

		public bool SerializeActiveAction(SimpleJSON.JSONNode node) {
			if (ActiveAction == null) { return false; }
			node.Add("action_id", ActiveAction.id);
			node.Add("action_age", ActiveActionAge);
			foreach (var a in actions) {
				node.Add($"action_{a.id}_score", a.lastCalculatedScore);
				foreach (var c in a.considerations) {
					node.Add($"action_{a.id}_consider_{c.id}", c.lastScore);
				}
			}
			return true;
		}

		public bool DeserializeActiveAction(SimpleJSON.JSONNode node) {
			if (!node.HasKey("action_id")) { return false; }
			foreach (var a in actions) {
				a.lastCalculatedScore = node[$"action_{a.id}_score"].AsFloat;
				foreach (var c in a.considerations) {
					c.lastScore = node[$"action_{a.id}_consider_{c.id}"];
				}
			}
			var id = node["action_id"].Value;
			ThisAction = ActiveAction = actions.Find(a => a.id == id);
			actionChangeTime = Time.time - node["action_age"].AsFloat;
			ActiveAction.onStart?.Invoke(target);
			OnActionChange?.Invoke(target, null, ActiveAction);
			ThisAction = null;
			return true;
		}
	}

}
