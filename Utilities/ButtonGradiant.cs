using g3;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UIElements;

namespace Vaga.Utilities
{
    public class ButtonGradiant : MonoBehaviour
    {
        public float speed = 0.4f;

        private Renderer _renderer;

        private void Start()
        {
            _renderer = this.GetComponent<Renderer>();
        }

        private void Update()
        {
            float num = Mathf.PingPong(Time.time * speed, 1f);
            _renderer.material.color = Color.Lerp(new Color(33f / 255f, 33f / 255f, 33f / 255f), new Color(19f / 255f, 19f / 255f, 19f / 255f), num);
        }
    }
}