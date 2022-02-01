# sus
Simple utility-theory based AI system for Unity/C#

Usage:

```C#
  SUS.Decider<GameObject> decider;
  
  void Start() {
    decider = new SUS.Decider<GameObject>(gameObject);

    decider.AddAction(
        "A",
        go => { go.GetComponent<Renderer>().material.color = Color.red; }, // onStart
        go => { go.transform.localScale += Vector3.one * Time.deltaTime; }, // onUpdate
        go => { go.transform.localScale = Vector3.one; } // onStop
      ).Considerations( 
        decider.Consider(go => 0.75f * Random.value))
      .ScoreCalculationTime(0.25f)
      .UserData("red");

    decider.AddAction(
        "B",
        go => { go.GetComponent<Renderer>().material.color = Color.green; } // onStart
      ).Considerations( 
        decider.Consider(go => (Time.time * 0.5f) % 1f))
      .ScoreCalculationTime(0.25f)
      .UserData("green");

    decider.OnActionChange += (go, prevAction, nextAction) => Debug.Log(nextAction.userData);
  }

  void Update() {
    decider.Update(Time.deltaTime);
  }
```
