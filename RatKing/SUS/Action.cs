#if UNITY_EDITOR
#define ALLOW_IMGUI_DEBUG
#endif

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

	public class Action {
		public System.Action onStart;
		public System.Action onUpdate;
		public System.Action onStop;
		public Consideration[] considerations;
		public Base.RangeFloat scoreCalculationMinMax = new Base.RangeFloat(0f, 1f);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public float scoreCalculationTime = 0f;
		public float lastCalculatedScore = 0f;
		public string id;
		public object userData;
		public bool getsConsidered = true;
		//
		float curScoreCalculateTime = 0f;

		//

		public Action(string id = null, System.Action onStart = null, System.Action onUpdate = null, System.Action onStop = null) {
			this.id = id;
			this.onStart = onStart;
			this.onUpdate = onUpdate;
			this.onStop = onStop;
		}

		public Action Considerations(params Consideration[] considerations) {
			this.considerations = considerations;
			return this;
		}

		public Action ScoreCalculationMinMax(Base.RangeFloat range) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(range, lastCalculatedScore);
			this.scoreCalculationMinMax = range;
			return this;
		}

		public Action ScoreCalculationMinMax(float min, float max) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(min, max, lastCalculatedScore);
			this.scoreCalculationMinMax = new Base.RangeFloat(min, max);
			return this;
		}

		public Action ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = Random.value * scoreCalculationTime; }
			return this;
		}

		public Action UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(float dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0f;
			var considerationCount = considerations.Length;
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
					default: lastCalculatedScore = (curCon.lastScore = curCon.function()); break;
					case ScoreCalculationMethod.Average: lastCalculatedScore = (curCon.lastScore = curCon.function()) / considerationCount; break;
				}
				for (int i = 1; i < considerationCount; ++i) {
					curCon = considerations[i];
					switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function()); break;
						case ScoreCalculationMethod.Max: lastCalculatedScore = Mathf.Max(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = Mathf.Min(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function()); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function()) / considerationCount; break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
		}

		void CalculateScoreMultiply() {
			lastCalculatedScore = 1f;
			foreach (var c in considerations) {
				c.lastScore = c.function();
				if (c.lastScore == 0f) { lastCalculatedScore = 0f; break; }
				lastCalculatedScore *= c.lastScore;
			}
		}

		void CalculateScoreMax() {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore = Mathf.Max(lastCalculatedScore, c.lastScore = c.function());
			}
		}

		void CalculateScoreMin() {
			lastCalculatedScore = float.PositiveInfinity;
			foreach (var c in considerations) {
				lastCalculatedScore = Mathf.Min(lastCalculatedScore, c.lastScore = c.function());
			}
		}

		void CalculateScoreAdd() {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore += c.lastScore = c.function();
			}
		}

		void CalculateScoreAverage() {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore += c.lastScore = c.function();
			}
			lastCalculatedScore /= considerations.Length;
		}
		
		//
		
		public float GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

	public class Action<T> {
		public System.Action<T> onStart;
		public System.Action<T> onUpdate;
		public System.Action<T> onStop;
		public Consideration<T>[] considerations;
		public Base.RangeFloat scoreCalculationMinMax = new Base.RangeFloat(0f, 1f);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public float scoreCalculationTime = 0f;
		public float lastCalculatedScore = 0f;
		public string id;
		public object userData;
		public bool getsConsidered = true;
		//
		float curScoreCalculateTime = 0f;

		//

		public Action(string id = null, System.Action<T> onStart = null, System.Action<T> onUpdate = null, System.Action<T> onStop = null) {
			this.id = id;
			this.onStart = onStart;
			this.onUpdate = onUpdate;
			this.onStop = onStop;
		}

		public Action<T> Considerations(params Consideration<T>[] considerations) {
			this.considerations = considerations;
			return this;
		}

		public Action<T> ScoreCalculationMinMax(Base.RangeFloat range) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(range, lastCalculatedScore);
			this.scoreCalculationMinMax = range;
			return this;
		}

		public Action<T> ScoreCalculationMinMax(float min, float max) {
			lastCalculatedScore = scoreCalculationMinMax.RemapTo(min, max, lastCalculatedScore);
			this.scoreCalculationMinMax = new Base.RangeFloat(min, max);
			return this;
		}

		public Action<T> ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action<T> ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = Random.value * scoreCalculationTime; }
			return this;
		}

		public Action<T> UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(ref T target, float dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0f;
			var considerationCount = considerations.Length;
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
					default: lastCalculatedScore = (curCon.lastScore = curCon.function(target)); break;
					case ScoreCalculationMethod.Average: lastCalculatedScore = (curCon.lastScore = curCon.function(target)) / considerationCount; break;
				}
				for (int i = 1; i < considerationCount; ++i) {
					curCon = considerations[i];
					switch (curCon.method == ScoreCalculationMethod.Standard ? scoreCalculationStandardMethod : curCon.method) {
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function(target)); break;
						case ScoreCalculationMethod.Max: lastCalculatedScore = Mathf.Max(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = Mathf.Min(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function(target)); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function(target)) / considerationCount; break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.Lerp(lastCalculatedScore);
			}
		}

		void CalculateScoreMultiply(ref T target) {
			lastCalculatedScore = 1f;
			foreach (var c in considerations) {
				c.lastScore = c.function(target);
				if (c.lastScore == 0f) { lastCalculatedScore = 0f; break; }
				lastCalculatedScore *= c.lastScore;
			}
		}

		void CalculateScoreMax(ref T target) {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore = Mathf.Max(lastCalculatedScore, c.lastScore = c.function(target));
			}
		}

		void CalculateScoreMin(ref T target) {
			lastCalculatedScore = float.PositiveInfinity;
			foreach (var c in considerations) {
				lastCalculatedScore = Mathf.Min(lastCalculatedScore, c.lastScore = c.function(target));
			}
		}

		void CalculateScoreAdd(ref T target) {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore += c.lastScore = c.function(target);
			}
		}

		void CalculateScoreAverage(ref T target) {
			lastCalculatedScore = 0f;
			foreach (var c in considerations) {
				lastCalculatedScore += c.lastScore = c.function(target);
			}
			lastCalculatedScore /= considerations.Length;
		}
		
		//
		
		public float GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

}
