/*
 * Copyright (c) 2014-2023 Pim de Witte All rights reserved.
 *
 * @author Pim de Witte (pimdewitte.com)
 * @version 1.0
 */

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

/// <summary>
/// A thread-safe class which holds a queue with actions to execute on the next Update() method. It can be used to make calls to the main thread for things such as UI Manipulation in Unity. A Singleton pattern is used.
/// </summary>
public class UnityMainThreadDispatcher : MonoBehaviour {
    private static readonly Queue<Action> _executionQueue = new Queue<Action>();
    private static UnityMainThreadDispatcher _instance = null;

    public static UnityMainThreadDispatcher Instance() {
        if (!_instance) {
            _instance = FindObjectOfType(typeof(UnityMainThreadDispatcher)) as UnityMainThreadDispatcher;
            if (!_instance) {
                Debug.LogWarning("No UnityMainThreadDispatcher found in scene. Creating one...");
                GameObject go = new GameObject("UnityMainThreadDispatcher");
                _instance = go.AddComponent<UnityMainThreadDispatcher>();
                DontDestroyOnLoad(go);
            }
        }
        return _instance;
    }

    void Awake() {
        if (_instance == null) {
            _instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
    }

    void Update() {
        lock(_executionQueue) {
            while (_executionQueue.Count > 0) {
                _executionQueue.Dequeue().Invoke();
            }
        }
    }

    /// <summary>
    /// Locks the queue and adds the Action to the queue
    /// </summary>
    /// <param name="action">Function that will be executed from the main thread.</param>
    public void Enqueue(Action action) {
        lock (_executionQueue) {
            _executionQueue.Enqueue(action);
        }
    }

    /// <summary>
    /// Executes an Action on the main thread
    /// </summary>
    /// <param name="action">The action to execute on the main thread.</param>
    /// <returns>A task that represents the completion of the action.</returns>
    public Task EnqueueAsync(Action action) {
        var tcs = new TaskCompletionSource<bool>();
        
        lock (_executionQueue) {
            _executionQueue.Enqueue(() => {
                try {
                    action();
                    tcs.TrySetResult(true);
                } catch (Exception ex) {
                    tcs.TrySetException(ex);
                }
            });
        }
        
        return tcs.Task;
    }
}