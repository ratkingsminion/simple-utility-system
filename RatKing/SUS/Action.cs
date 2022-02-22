#if UNITY_EDITOR
#define ALLOW_IMGUI_DEBUG
#endif

using System.Collections.Generic;
using UnityEngine;

namespace RatKing.SUS {

	public enum ScoreCalculationMethod {
		Multiply,
		Max,
		Min,
		Add,
		Average,
		Standard
	}

	public static class ExtensionMethods {
		public static string ToShortString(this ScoreCalculationMethod method) {
			return method == ScoreCalculationMethod.Multiply ? "MUL" :
				   method == ScoreCalculationMethod.Max ? "MAX" :
				   method == ScoreCalculationMethod.Min ? "MIN" :
				   method == ScoreCalculationMethod.Add ? "ADD" :
				   method == ScoreCalculationMethod.Average ? "AVG" : "STD";
		}
	}

	//

	public class Action<TId> {
		public System.Action onStart;
		public System.Action onUpdate;
		public System.Action onStop;
		public List<Consideration> considerations = new List<Consideration>();
		public Base.RangeFloat scoreCalculationMinMax = new Base.RangeFloat(0f, 1f);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public float scoreCalculationTime = 0f;
		public float lastCalculatedScore = 0f;
		public TId id;
		public object userData;
		public bool getsConsidered = true;
		//
		float curScoreCalculateTime = 0f;

		//

		public Action(TId id = default, System.Action onStart = null, System.Action onUpdate = null, System.Action onStop = null) {
			this.id = id;
			this.onStart = onStart;
			this.onUpdate = onUpdate;
			this.onStop = onStop;
		}
		
		/// <summary>
		/// change the function that gets called on start
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnStart(System.Action onStart) {
			this.onStart = onStart;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on update
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnUpdate(System.Action onUpdate) {
			this.onUpdate = onUpdate;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on stop
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnStop(System.Action onStop) {
			this.onStop = onStop;
			return this;
		}
			
		/// <summary>
		/// add several considerations
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> Considerations(params Consideration[] considerations) {
			this.considerations.AddRange(considerations);
			return this;
		}
		
		/// <summary>
		/// add a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(string id, System.Func<float> function) {
			considerations.Add(new Consideration(id, function));
			return this;
		}

		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(System.Func<float> function) {
			considerations.Add(new Consideration(function));
			return this;
		}

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(string id, System.Func<float> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration(id, function, calculationMethod));
			return this;
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(System.Func<float> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration(function, calculationMethod));
			return this;
		}

		public Action<TId> ScoreCalculationMinMax(Base.RangeFloat range) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(range, lastCalculatedScore);
			this.scoreCalculationMinMax = range;
			return this;
		}

		public Action<TId> ScoreCalculationMinMax(float min, float max) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(min, max, lastCalculatedScore);
			this.scoreCalculationMinMax = new Base.RangeFloat(min, max);
			return this;
		}

		public Action<TId> ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action<TId> ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = Random.value * scoreCalculationTime; }
			return this;
		}

		public Action<TId> UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(float dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0f;
			var considerationCount = considerations.Count;
			if (considerationCount == 0) {
				lastCalculatedScore = scoreCalculationMinMax.min;
			}
			else if (considerationCount == 1) {
				lastCalculatedScore = considerations[0].lastScore = considerations[0].function();
				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
			else {
				var curCon = considerations[0];
				switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
					case ScoreCalculationMethod.Average: lastCalculatedScore = (curCon.lastScore = curCon.function()) / considerationCount; break;
					default: lastCalculatedScore = (curCon.lastScore = curCon.function()); break;
				}
				for (int i = 1; i < considerationCount; ++i) {
					curCon = considerations[i];
					switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
						case ScoreCalculationMethod.Max: lastCalculatedScore = Mathf.Max(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = Mathf.Min(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function()); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function()) / considerationCount; break;
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function()); break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
		}
		
		//
		
		public float GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

	public class Action<TTarget, TId> {
		public System.Action<TTarget> onStart;
		public System.Action<TTarget> onUpdate;
		public System.Action<TTarget> onStop;
		public List<Consideration<TTarget>> considerations = new List<Consideration<TTarget>>();
		public Base.RangeFloat scoreCalculationMinMax = new Base.RangeFloat(0f, 1f);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public float scoreCalculationTime = 0f;
		public float lastCalculatedScore = 0f;
		public TId id;
		public object userData;
		public bool getsConsidered = true;
		//
		float curScoreCalculateTime = 0f;

		//

		public Action(TId id = default, System.Action<TTarget> onStart = null, System.Action<TTarget> onUpdate = null, System.Action<TTarget> onStop = null) {
			this.id = id;
			this.onStart = onStart;
			this.onUpdate = onUpdate;
			this.onStop = onStop;
		}
		
		/// <summary>
		/// change the function that gets called on start
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnStart(System.Action<TTarget> onStart) {
			this.onStart = onStart;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on update
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnUpdate(System.Action<TTarget> onUpdate) {
			this.onUpdate = onUpdate;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on stop
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnStop(System.Action<TTarget> onStop) {
			this.onStop = onStop;
			return this;
		}
			
		/// <summary>
		/// add several considerations
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Considerations(params Consideration<TTarget>[] considerations) {
			this.considerations.AddRange(considerations);
			return this;
		}
		
		/// <summary>
		/// add a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(string id, System.Func<TTarget, float> function) {
			considerations.Add(new Consideration<TTarget>(id, function));
			return this;
		}

		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(System.Func<TTarget, float> function) {
			considerations.Add(new Consideration<TTarget>(function));
			return this;
		}

		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="id">name that will be shown in the debug display</param>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(string id, System.Func<TTarget, float> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration<TTarget>(id, function, calculationMethod));
			return this;
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(System.Func<TTarget, float> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration<TTarget>(function, calculationMethod));
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationMinMax(Base.RangeFloat range) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(range, lastCalculatedScore);
			this.scoreCalculationMinMax = range;
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationMinMax(float min, float max) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(min, max, lastCalculatedScore);
			this.scoreCalculationMinMax = new Base.RangeFloat(min, max);
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = Random.value * scoreCalculationTime; }
			return this;
		}

		public Action<TTarget, TId> UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(ref TTarget target, float dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0f;
			var considerationCount = considerations.Count;
			if (considerationCount == 0) {
				lastCalculatedScore = scoreCalculationMinMax.min;
			}
			else if (considerationCount == 1) {
				lastCalculatedScore = considerations[0].lastScore = considerations[0].function(target);
				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
			else {
				var curCon = considerations[0];
				switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
					case ScoreCalculationMethod.Average: lastCalculatedScore = (curCon.lastScore = curCon.function(target)) / considerationCount; break;
					default: lastCalculatedScore = (curCon.lastScore = curCon.function(target)); break;
				}
				for (int i = 1; i < considerationCount; ++i) {
					curCon = considerations[i];
					switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
						case ScoreCalculationMethod.Max: lastCalculatedScore = Mathf.Max(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = Mathf.Min(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function(target)); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function(target)) / considerationCount; break;
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function(target)); break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
		}
		
		//
		
		public float GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

}
