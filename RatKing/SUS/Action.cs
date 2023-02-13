#if UNITY_EDITOR
#define ALLOW_IMGUI_DEBUG
#endif

using System.Collections.Generic;

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
		public System.Action start;
		public System.Action update;
		public System.Action stop;
		public List<Consideration> considerations = new List<Consideration>();
		public (double min, double max) scoreCalculationMinMax = (0.0, 1.0);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public double scoreCalculationTime = 0.0;
		public double lastCalculatedScore = 0.0;
		public TId id;
		public object userData;
		public bool getsConsidered = true;
		
		double curScoreCalculateTime = 0.0;
		readonly static System.Random random = new System.Random();

		//

		public Action(TId id = default, System.Action start = null, System.Action update = null, System.Action stop = null) {
			this.id = id;
			this.start = start;
			this.update = update;
			this.stop = stop;
		}
		
		/// <summary>
		/// change the function that gets called on start
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnStart(System.Action start) {
			this.start = start;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on update
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnUpdate(System.Action update) {
			this.update = update;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on stop
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TId> OnStop(System.Action stop) {
			this.stop = stop;
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
		public Action<TId> Consider(string id, System.Func<double> function) {
			considerations.Add(new Consideration(id, function));
			return this;
		}

		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(System.Func<double> function) {
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
		public Action<TId> Consider(string id, System.Func<double> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration(id, function, calculationMethod));
			return this;
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TId> Consider(System.Func<double> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration(function, calculationMethod));
			return this;
		}

		public Action<TId> ScoreCalculationMinMax(double min, double max) {
			lastCalculatedScore = ((max - min) * (lastCalculatedScore - scoreCalculationMinMax.min) / (scoreCalculationMinMax.max - scoreCalculationMinMax.min)) + min;
			this.scoreCalculationMinMax = (min, max);
			return this;
		}

		public Action<TId> ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action<TId> ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = (float)random.NextDouble() * scoreCalculationTime; }
			return this;
		}

		public Action<TId> UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(double dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0.0;
			var considerationCount = considerations.Count;
			if (considerationCount == 0) {
				lastCalculatedScore = scoreCalculationMinMax.min;
			}
			else if (considerationCount == 1) {
				lastCalculatedScore = considerations[0].lastScore = considerations[0].function();
				lastCalculatedScore = scoreCalculationMinMax.min + (scoreCalculationMinMax.max - scoreCalculationMinMax.min) * lastCalculatedScore;
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
						case ScoreCalculationMethod.Max: lastCalculatedScore = System.Math.Max(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = System.Math.Min(lastCalculatedScore, (curCon.lastScore = curCon.function())); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function()); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function()) / considerationCount; break;
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function()); break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.min + (scoreCalculationMinMax.max - scoreCalculationMinMax.min) * lastCalculatedScore;
			}
		}
		
		//
		
		public double GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

	public class Action<TTarget, TId> {
		public System.Action<TTarget> start;
		public System.Action<TTarget> update;
		public System.Action<TTarget> stop;
		public List<Consideration<TTarget>> considerations = new List<Consideration<TTarget>>();
		public (double min, double max) scoreCalculationMinMax = (0.0, 1.0);
		public ScoreCalculationMethod scoreCalculationStandardMethod = ScoreCalculationMethod.Multiply;
		public double scoreCalculationTime = 0.0;
		public double lastCalculatedScore = 0.0;
		public TId id;
		public object userData;
		public bool getsConsidered = true;

		double curScoreCalculateTime = 0.0;
		readonly static System.Random random = new System.Random();

		//

		public Action(TId id = default, System.Action<TTarget> start = null, System.Action<TTarget> update = null, System.Action<TTarget> stop = null) {
			this.id = id;
			this.start = start;
			this.update = update;
			this.stop = stop;
		}
		
		/// <summary>
		/// change the function that gets called on start
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnStart(System.Action<TTarget> start) {
			this.start = start;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on update
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnUpdate(System.Action<TTarget> update) {
			this.update = update;
			return this;
		}
		
		/// <summary>
		/// change the function that gets called on stop
		/// </summary>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> OnStop(System.Action<TTarget> stop) {
			this.stop = stop;
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
		public Action<TTarget, TId> Consider(string id, System.Func<TTarget, double> function) {
			considerations.Add(new Consideration<TTarget>(id, function));
			return this;
		}

		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(System.Func<TTarget, double> function) {
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
		public Action<TTarget, TId> Consider(string id, System.Func<TTarget, double> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration<TTarget>(id, function, calculationMethod));
			return this;
		}
		
		/// <summary>
		/// helper function to create a consideration
		/// </summary>
		/// <param name="function">function that creates a score</param>
		/// <param name="calculationMethod">how to calculate this consideration (in relation to the other considerations)</param>
		/// <returns>the action, for currying</returns>
		public Action<TTarget, TId> Consider(System.Func<TTarget, double> function, ScoreCalculationMethod calculationMethod) {
			considerations.Add(new Consideration<TTarget>(function, calculationMethod));
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationMinMax(double min, double max) {
			lastCalculatedScore = ((max - min) * (lastCalculatedScore - scoreCalculationMinMax.min) / (scoreCalculationMinMax.max - scoreCalculationMinMax.min)) + min;
			this.scoreCalculationMinMax = (min, max);
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationStandardMethod(ScoreCalculationMethod scoreCalculationMethod) {
			this.scoreCalculationStandardMethod = scoreCalculationMethod;
			return this;
		}

		public Action<TTarget, TId> ScoreCalculationTime(float scoreCalculationTime, bool randomizeStartTime = true) {
			this.scoreCalculationTime = scoreCalculationTime;
			if (randomizeStartTime) { this.curScoreCalculateTime = (float)random.NextDouble() * scoreCalculationTime; }
			return this;
		}

		public Action<TTarget, TId> UserData(object userData) {
			this.userData = userData;
			return this;
		}

		//

		public void Calculate(ref TTarget target, double dt) {
			curScoreCalculateTime += dt;
			if (curScoreCalculateTime < scoreCalculationTime) { return; }
			curScoreCalculateTime = 0.0;
			var considerationCount = considerations.Count;
			if (considerationCount == 0) {
				lastCalculatedScore = scoreCalculationMinMax.min;
			}
			else if (considerationCount == 1) {
				lastCalculatedScore = considerations[0].lastScore = considerations[0].function(target);
				lastCalculatedScore = scoreCalculationMinMax.min + (scoreCalculationMinMax.max - scoreCalculationMinMax.min) * lastCalculatedScore;
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
						case ScoreCalculationMethod.Max: lastCalculatedScore = System.Math.Max(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Min: lastCalculatedScore = System.Math.Min(lastCalculatedScore, (curCon.lastScore = curCon.function(target))); break;
						case ScoreCalculationMethod.Add: lastCalculatedScore += (curCon.lastScore = curCon.function(target)); break;
						case ScoreCalculationMethod.Average: lastCalculatedScore += (curCon.lastScore = curCon.function(target)) / considerationCount; break;
						default: case ScoreCalculationMethod.Multiply: lastCalculatedScore *= (curCon.lastScore = curCon.function(target)); break;
					}
				}

				lastCalculatedScore = scoreCalculationMinMax.min + (scoreCalculationMinMax.max - scoreCalculationMinMax.min) * lastCalculatedScore;
			}
		}
		
		//
		
		public double GetRemainingCalculationTime() {
			return scoreCalculationTime - curScoreCalculateTime;
		}
	}

}
