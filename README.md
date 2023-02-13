# sus
Simple utility-theory based AI system, usable with Unity

Usage (Unity):

```C#
  // a decider optionally can have a target (in this case UnityEngine.GameObject)
  SUS.Decider<GameObject> decider;
  
  void Start() {
    decider = new SUS.Decider<GameObject>(gameObject);
    
    // action creation type A - callbacks added via AddAction()
    decider.AddAction(
        "A",
        go => { go.GetComponent<Renderer>().material.color = Color.red; }, // onStart
        go => { go.transform.localScale += Vector3.one * Time.deltaTime; }, // onUpdate
        go => { go.transform.localScale = Vector3.one; } // onStop
      )
      .Consider(go => 0.75 * Random.value)
      .ScoreCalculationTime(0.25)
      .UserData("red");
    
    // action creation type B - add callbacks via currying
    decider.AddAction("B")
      .OnStart(go => { go.GetComponent<Renderer>().material.color = Color.green; })
      .Consider(go => (Time.time * 0.5) % 1.0)
      .ScoreCalculationTime(0.25)
      .UserData("green");
    
    decider.OnActionChange += (go, prevAction, nextAction) => Debug.Log(nextAction.userData);
  }

  void Update() {
    decider.Update(Time.deltaTime);
  }
```
