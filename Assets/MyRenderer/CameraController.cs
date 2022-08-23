using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;

namespace MyRenderer
{
    public class CameraController : MonoBehaviour
    {
        public float MoveSpeed = 1.0f;
        public float RotateSpeed = 1.0f;
        
        private Inputs _inputs;
        private Transform _camTransform;
        private Vector2 _rotate;

        private void Awake()
        {
            _inputs = new Inputs();
        }

        private void OnEnable()
        {
            _inputs.Enable();
        }

        private void OnDisable()
        {
            _inputs.Disable();
        }

        private void Start()
        {
            var cam = GetComponent<Camera>();
            _camTransform = cam.transform;
        }

        private void Update()
        {
            var move = _inputs.Camera.Move.ReadValue<Vector2>();
            var look = _inputs.Camera.Look.ReadValue<Vector2>();

            var moveSpeed = MoveSpeed * Time.deltaTime;
            _camTransform.position += _camTransform.rotation * new Vector3(move.x, 0.0f, move.y) * moveSpeed;

            var rotateSpeed = RotateSpeed * Time.deltaTime;
            _rotate.y += rotateSpeed * look.x;
            _rotate.x = Mathf.Clamp(_rotate.x - rotateSpeed * look.y, -89.9f, 89.9f);
            _camTransform.localEulerAngles = _rotate;
        }
    }
}