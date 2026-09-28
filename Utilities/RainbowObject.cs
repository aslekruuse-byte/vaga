using g3;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vaga.Utilities
{
    public class RainbowObject : MonoBehaviour
    {
        public float speed = 0.4f;

        private Renderer _renderer;

        private void Start()
        {
            _renderer = ((Component)this).GetComponent<Renderer>();
        }

        private void Update()
        {
            float num = Mathf.PingPong(Time.time * speed, 1f);
            _renderer.material.color = Color.Lerp(new Color(0.235f, 0f, 0.784f), new Color(19f / 255f, 19f / 255f, 19f / 255f), num);
        }
    }
}
