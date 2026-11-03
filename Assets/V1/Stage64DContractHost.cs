#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
// Owns only the reviewed test coroutine, never actor movement or gameplay state.
public sealed class Stage64DContractHost : MonoBehaviour
{
    public bool Complete {get;private set;}
    public Exception Error {get;private set;}
    InputSettings originalInput,ownedInput;bool originalBackground;
    void Awake()
    {
        if(!Application.isPlaying||!SporeGobbo.Safety.Persistence.Isolated||!V1RuntimeProfile.DevelopmentStorage)throw new InvalidOperationException("Owned editor contract host only");
        originalBackground=Application.runInBackground;Application.runInBackground=true;
        originalInput=InputSystem.settings;ownedInput=Instantiate(originalInput);ownedInput.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
        ownedInput.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.settings=ownedInput;
    }
    void OnDestroy(){if(originalInput!=null)InputSystem.settings=originalInput;if(ownedInput!=null)Destroy(ownedInput);Application.runInBackground=originalBackground;}
    public void Begin(IEnumerator routine)=>StartCoroutine(Run(routine));
    IEnumerator Run(IEnumerator routine)
    {
        var stack=new Stack<IEnumerator>();stack.Push(routine);
        try
        {
            while(stack.Count>0)
            {
                bool more=false;object current=null;
                try{more=stack.Peek().MoveNext();if(more)current=stack.Peek().Current;}
                catch(Exception error){Error=error;break;}
                if(!more){(stack.Pop() as IDisposable)?.Dispose();continue;}
                if(current is IEnumerator nested){stack.Push(nested);continue;}
                yield return current;
            }
        }
        finally
        {
            while(stack.Count>0)(stack.Pop() as IDisposable)?.Dispose();
            Complete=true;
        }
    }
}
#endif
