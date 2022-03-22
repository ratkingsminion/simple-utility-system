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
		readonly List<DebugDraw> debugDraws = new List<DebugDraw>();
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

	public class Decider<TId> {

		public Action<TId> ActiveAction { get; private set; } = null;
		public Action<TId> ConsideredAction { get; private set; } = null; // only valid during consideration!
		public Action<TId> ThisAction { get; private set; } = null; // only valid during consideration and execution of active action!
		public float ActiveActionAge => Time.time - actionChangeTime;
		public bool IsConsideringActiveAction => ActiveAction == ConsideredAction;
		public event System.Action<Action<TId>, Action<TId>> OnActionChange = null; // target, prevAction, nextAction
		readonly List<Action<TId>> actions = new List<Action<TId>>();
		float actionChangeTime = 0f;
		Action<TId> forcedAction = null;

		//

#if ALLOW_IMGUI_DEBUG
		public Decider(DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { DeciderDebugDraw.Inst.AddDebugDraw(this, DebugGUI, debugDisplayMode); }
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
			
			Action<TId> chosenAction = null;
			if (forcedAction != null) {
				// clear scores
				foreach (var pa in actions) { pa.lastCalculatedScore = pa.scoreCalculationMinMax.Lerp(pa == forcedAction ? 1f : 0f); }
				chosenAction = forcedAction;
				forcedAction = null;
			}
			else {
				// calculate scores
				foreach (var pa in actions) {
					if (!pa.getsConsidered) { continue; }
					ThisAction = ConsideredAction = pa;
					pa.Calculate(dt);
				}
				ThisAction = ConsideredAction = null;

				// choose action
				var score = float.NegativeInfinity;
				foreach (var pa in actions) {
					if (pa.lastCalculatedScore > score) {
						chosenAction = pa;
						score = pa.lastCalculatedScore;
					}
				}
			}

			// switch current action
			ThisAction = ActiveAction;
			if (ActiveAction != chosenAction) {
				actionChangeTime = Time.time;
				var prevAction = ActiveAction;
				ActiveAction?.stop?.Invoke();
				ThisAction = ActiveAction = chosenAction;
				OnActionChange?.Invoke(prevAction, ActiveAction);
				chosenAction.start?.Invoke();
			}
			if (ActiveAction != null) {
				ActiveAction.update?.Invoke();
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
				if (!a.id.Equals(default(TId))) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime():0.00})"; }
				text += $" [{a.scoreCalculationStandardMethod.ToShortString()}]";
				for (int i = 0; i < a.considerations.Count; ++i) {
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
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="onStop">called when this action becomes un-chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<TId> AddAction(TId id, System.Action onStart, System.Action onUpdate, System.Action onStop, params Consideration[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id, onStart, onUpdate, onStop);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
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
		public Action<TId> AddAction(TId id, System.Action onStart, System.Action onUpdate, params Consideration[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id, onStart, onUpdate);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
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
		public Action<TId> AddAction(TId id, System.Action onStart, params Consideration[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id, onStart);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<TId> AddAction(TId id, params Consideration[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <returns>the created and added possible action</returns>
		public Action<TId> AddAction(TId id) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id);
			actions.Add(pa);
			return pa;
		}

		/// <summary>
		/// Changes the action on the next Update call to this one, overriding all considerations for one tick
		/// </summary>
		/// <param name="id">name of the action to force onto the decider</param>
		/// <returns></returns>
		public bool ForceAction(TId id) {
			var action = actions.Find(a => a.id.Equals(id));
			if (action == null) { return false; }
			forcedAction = action;
			return true;
		}

		/// <summary>
		/// Changes the action on the next Update call to this one, overriding all considerations for one tick
		/// </summary>
		/// <param name="action">the action to force onto the decider</param>
		/// <returns></returns>
		public void ForceAction(Action<TId> action) {
			forcedAction = action;
		}
	}

	public class Decider<TTarget, TId> where TTarget : class {
		TTarget target;
		public TTarget Target => target;
		public Action<TTarget, TId> ActiveAction { get; private set; } = null;
		public Action<TTarget, TId> ConsideredAction { get; private set; } = null; // only valid during consideration!
		public Action<TTarget, TId> ThisAction { get; private set; } = null; // only valid during consideration and execution of active action!
		public float ActiveActionAge => Time.time - actionChangeTime;
		public bool IsConsideringActiveAction => ActiveAction == ConsideredAction;
		public event System.Action<TTarget, Action<TTarget, TId>, Action<TTarget, TId>> OnActionChange = null; // target, prevAction, nextAction
		readonly List<Action<TTarget, TId>> actions = new List<Action<TTarget, TId>>();
		float actionChangeTime = 0f;
		Action<TTarget, TId> forcedAction = null;

		//

#if ALLOW_IMGUI_DEBUG
		public Decider(TTarget target, DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { DeciderDebugDraw.Inst.AddDebugDraw(target, DebugGUI, debugDisplayMode); }
			this.target = target;
			this.actionChangeTime = Time.time;
		}
#else
		public Decider(TTarget target, DebugDisplayMode debugDisplayMode = DebugDisplayMode.None) {
			if (debugDisplayMode != DebugDisplayMode.None) { Debug.Log("Debug display for Decider not allowed."); }
			this.target = target;
			this.actionChangeTime = Time.time;
		}
#endif

		public void Update(float dt) {
			if (actions.Count == 0) { return; }
			
			Action<TTarget, TId> chosenAction = null;
			if (forcedAction != null) {
				// clear scores
				foreach (var pa in actions) { pa.lastCalculatedScore = pa.scoreCalculationMinMax.Lerp(pa == forcedAction ? 1f : 0f); }
				chosenAction = forcedAction;
				forcedAction = null;
			}
			else {
				// calculate scores
				foreach (var pa in actions) {
					if (!pa.getsConsidered) { continue; }
					ThisAction = ConsideredAction = pa;
					pa.Calculate(ref target, dt);
				}
				ThisAction = ConsideredAction = null;

				// choose action
				var score = float.NegativeInfinity;
				foreach (var pa in actions) {
					if (pa.lastCalculatedScore > score) {
						chosenAction = pa;
						score = pa.lastCalculatedScore;
					}
				}
			}

			// switch current action
			ThisAction = ActiveAction;
			if (ActiveAction != chosenAction) {
				actionChangeTime = Time.time;
				var prevAction = ActiveAction;
				ActiveAction?.stop?.Invoke(target);
				ThisAction = ActiveAction = chosenAction;
				OnActionChange?.Invoke(target, prevAction, ActiveAction);
				chosenAction.start?.Invoke(target);
			}
			if (ActiveAction != null) {
				ActiveAction.update?.Invoke(target);
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
				if (!a.id.Equals(default(TId))) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime():0.00})"; }
				text += $" [{a.scoreCalculationStandardMethod.ToShortString()}]";
				for (int i = 0; i < a.considerations.Count; ++i) {
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
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="onStart">called when this action gets chosen</param>
		/// <param name="onUpdate">called as long this action is chosen</param>
		/// <param name="onStop">called when this action becomes un-chosen</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<TTarget, TId> AddAction(TId id, System.Action<TTarget> onStart, System.Action<TTarget> onUpdate, System.Action<TTarget> onStop, params Consideration<TTarget>[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id, onStart, onUpdate, onStop);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
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
		public Action<TTarget, TId> AddAction(TId id, System.Action<TTarget> onStart, System.Action<TTarget> onUpdate, params Consideration<TTarget>[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id, onStart, onUpdate);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
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
		public Action<TTarget, TId> AddAction(TId id, System.Action<TTarget> onStart, params Consideration<TTarget>[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id, onStart);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="considerations">considerations calculate the score; use decider.Consider() helper functions to create them</param>
		/// <returns>the created and added possible action</returns>
		public Action<TTarget, TId> AddAction(TId id, params Consideration<TTarget>[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id);
			if (considerations.Length > 0) { pa.considerations.AddRange(considerations); }
			actions.Add(pa);
			return pa;
		}
		
		/// <summary>
		/// Add a possible action that will be chosen when its score is high enough
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <returns>the created and added possible action</returns>
		public Action<TTarget, TId> AddAction(TId id) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id);
			actions.Add(pa);
			return pa;
		}

		/// <summary>
		/// Changes the action on the next Update call to this one, overriding all considerations for one tick
		/// </summary>
		/// <param name="id">name of the action to force onto the decider</param>
		/// <returns></returns>
		public bool ForceAction(TId id) {
			var action = actions.Find(a => a.id.Equals(id));
			if (action == null) { return false; }
			forcedAction = action;
			return true;
		}

		/// <summary>
		/// Changes the action on the next Update call to this one, overriding all considerations for one tick
		/// </summary>
		/// <param name="action">the action to force onto the decider</param>
		/// <returns></returns>
		public void ForceAction(Action<TTarget, TId> action) {
			forcedAction = action;
		}
		
		// saving/loading

		public bool SerializeActiveAction(SimpleJSON.JSONNode json) {
			if (ActiveAction == null) { return false; }
			json.Add("action_id", ActiveAction.id.ToString());
			json.Add("action_age", ActiveActionAge);
			foreach (var a in actions) {
				json.Add("action_" + a.id + "_score", a.lastCalculatedScore);
				foreach (var c in a.considerations) {
					json.Add("action_" + a.id + "_consider_" + c.id, c.lastScore);
				}
			}
			return true;
		}

		public bool DeserializeActiveAction(SimpleJSON.JSONNode json) {
			if (!json.HasKey("action_id")) { return false; }
			foreach (var a in actions) {
				a.lastCalculatedScore = json["action_" + a.id + "_score"].AsFloat;
				foreach (var c in a.considerations) {
					c.lastScore = json["action_" + a.id + "_consider_" + c.id];
				}
			}
			var id = json["action_id"].Value;
			ThisAction = ActiveAction = actions.Find(a => a.id.ToString() == id);
			actionChangeTime = Time.time - json["action_age"].AsFloat;
			ActiveAction.onStart?.Invoke(target);
			OnActionChange?.Invoke(target, null, ActiveAction); // TODO needed?
			ThisAction = null;
			return true;
		}
	}
	}

}
