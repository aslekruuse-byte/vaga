using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Vaga.Components
{
    public class CoroutineHandler : MonoBehaviour
    {
        private static CoroutineHandler? instance;

        public static CoroutineHandler Instance
        {
            get
            {
                if (instance == null)
                {
                    var ch = new GameObject("CoroutineHandler");
                    DontDestroyOnLoad(ch);
                    instance = ch.AddComponent<CoroutineHandler>();
                }
                return instance;
            }
        }

        public static Coroutine StartCoroutine1(IEnumerator routine)
        {
            return Instance.StartCoroutine(routine);
        }
    }
}
