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

			// calculate scores
			foreach (var pa in actions) {
				if (!pa.getsConsidered) { continue; }
				ThisAction = ConsideredAction = pa;
				pa.Calculate(dt);
			}
			ThisAction = ConsideredAction = null;

			// choose action
			Action<TId> chosenAction = null;
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
				if (!a.id.Equals(default(TId))) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime():0.00})"; }
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
		public Action<TId> AddAction(TId id, System.Action onStart, System.Action onUpdate, System.Action onStop, params Consideration[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TId>(id, onStart, onUpdate, onStop) { considerations = considerations };
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
			var pa = new Action<TId>(id, onStart, onUpdate) { considerations = considerations };
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
			var pa = new Action<TId>(id, onStart) { considerations = considerations };
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
			var pa = new Action<TId>(id) { considerations = considerations };
			actions.Add(pa);
			return pa;
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

			// calculate scores
			foreach (var pa in actions) {
				if (!pa.getsConsidered) { continue; }
				ThisAction = ConsideredAction = pa;
				pa.Calculate(ref target, dt);
			}
			ThisAction = ConsideredAction = null;

			// choose action
			Action<TTarget, TId> chosenAction = null;
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
				if (!a.id.Equals(default(TId))) { text += $" {a.id}"; }
				if (a.scoreCalculationTime > 0f) { text += $" ({a.GetRemainingCalculationTime():0.00})"; }
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
		public Consideration<TTarget> Consider(string id, System.Func<TTarget, float> function) {
			return new Consideration<TTarget>(id, function);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the new Consideration</returns>
		public Consideration<TTarget> Consider(System.Func<TTarget, float> function) {
			return new Consideration<TTarget>(function);
		}

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration<TTarget> Consider(string id, System.Func<TTarget, float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration<TTarget>(id, function, calculationMethod);
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the new Consideration</returns>
		public Consideration<TTarget> Consider(System.Func<TTarget, float> function, ScoreCalculationMethod calculationMethod) {
			return new Consideration<TTarget>(function, calculationMethod);
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
		public Action<TTarget, TId> AddAction(TId id, System.Action<TTarget> onStart, System.Action<TTarget> onUpdate, System.Action<TTarget> onStop, params Consideration<TTarget>[] considerations) {
			if (id.Equals(default(TId))) { Debug.Log("The default cannot be used as ID!"); return null; }
			var pa = new Action<TTarget, TId>(id, onStart, onUpdate, onStop) { considerations = considerations };
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
			var pa = new Action<TTarget, TId>(id, onStart, onUpdate) { considerations = considerations };
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
			var pa = new Action<TTarget, TId>(id, onStart) { considerations = considerations };
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
			var pa = new Action<TTarget, TId>(id) { considerations = considerations };
			actions.Add(pa);
			return pa;
		}
	}

}
