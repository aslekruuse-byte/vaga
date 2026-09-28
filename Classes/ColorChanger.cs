using Photon.Realtime;
﻿using UnityEngine;

namespace Vaga.Classes
{
    public class ColorChanger : TimedBehaviour
    {
        public Renderer renderer;
        public Renderer targetRenderer;
        public ExtGradient colors;
        public ExtGradient colorInfo;
        
        public void Start()
        {
            if (colors == null)
            {
                Destroy(this);
                return;
            }

            targetRenderer = GetComponent<Renderer>();

            if (colors.IsFlat())
            {
                Update();
                Destroy(this);
                return;
            }

            Update();
        }

        public void Update()
        {
            targetRenderer.enabled = !colors.transparent;

            if (colors.transparent)
                return;

            targetRenderer.material.color = colors.GetCurrentColor();
        }
    }
    public class TimedBehaviour : MonoBehaviour
    {
        public bool complete = false;

        public bool loop = true;

        public float progress = 0f;

        protected bool paused = false;

        protected float startTime;

        protected float duration = 2f;

        public virtual void Start()
        {
            startTime = Time.time;
        }

        public virtual void Update()
        {
            if (complete)
            {
                return;
            }
            progress = Mathf.Clamp((Time.time - startTime) / duration, 0f, 1f);
            if (Time.time - startTime > duration)
            {
                if (loop)
                {
                    OnLoop();
                }
                else
                {
                    complete = true;
                }
            }
        }

        public virtual void OnLoop()
        {
            startTime = Time.time;
        }
    }

}
