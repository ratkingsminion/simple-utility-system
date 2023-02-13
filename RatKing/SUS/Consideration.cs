namespace RatKing.SUS {
	
	public class Consideration {
		public string id;
		public System.Func<double> function;
		public double lastScore = 0f;
		public ScoreCalculationMethod method = ScoreCalculationMethod.Standard;
		public Consideration(string id, System.Func<double> function, ScoreCalculationMethod method) { this.id = id; this.function = function; this.method = method; }
		public Consideration(System.Func<double> function, ScoreCalculationMethod method) { this.id = null; this.function = function; this.method = method; }
		public Consideration(string id, System.Func<double> function) { this.id = id; this.function = function; }
		public Consideration(System.Func<double> function) { this.id = null; this.function = function; }

		//

		public override string ToString() {
			var str = lastScore.ToString("0.00");
			if (method != ScoreCalculationMethod.Standard) { str += $" [{method.ToShortString()}]"; }
			if (!string.IsNullOrEmpty(id)) { str += $" - {id}"; }
			return str;
		}
	}
	
	public class Consideration<T> {
		public string id;
		public System.Func<T, double> function;
		public double lastScore = 0f;
		public ScoreCalculationMethod method = ScoreCalculationMethod.Standard;
		public Consideration(string id, System.Func<T, double> function, ScoreCalculationMethod method) { this.id = id; this.function = function; this.method = method; }
		public Consideration(System.Func<T, double> function, ScoreCalculationMethod method) { this.id = null; this.function = function; this.method = method; }
		public Consideration(string id, System.Func<T, double> function) { this.id = id; this.function = function; }
		public Consideration(System.Func<T, double> function) { this.id = null; this.function = function; }

		//

		public override string ToString() {
			var str = lastScore.ToString("0.00");
			if (method != ScoreCalculationMethod.Standard) { str += $" [{method.ToShortString()}]"; }
			if (!string.IsNullOrEmpty(id)) { str += $" - {id}"; }
			return str;
		}
	}

}
