using UnityEngine;
using UnityEngine.InputSystem;

namespace University.Act01
{
    /// <summary>
    /// 第一幕的第一人称视线控制：只转视线，不平移。
    ///
    /// 结构（由 Tools/Act01/Player/Setup Rig 生成）：
    ///   PlayerRig (位置)
    ///   └ Yaw     ← 本脚本挂在这里，左右转
    ///     └ Pitch ← 上下转
    ///       └ Camera
    ///
    /// 之所以拆两层而不是把所有旋转塞给相机：单层做俯仰会在接近 ±90° 时出
    /// 现滚转（万向锁），而且动作和姿态混在一起，后面接 Timeline 机位动画不好切。
    /// </summary>
    [DisallowMultipleComponent]
    public class FirstPersonLook : MonoBehaviour
    {
        [Header("Pivot")]
        [SerializeField] Transform yawPivot;
        [SerializeField] Transform pitchPivot;

        [Header("Sensitivity")]
        [Tooltip("鼠标增量乘数。0.12 大约是「移动一整屏鼠标转 90 度」")]
        [SerializeField] float sensitivity = 0.12f;
        [SerializeField] bool invertY = false;

        [Header("Limits")]
        [Tooltip("相对初始朝向左右各能转多少度")]
        [SerializeField] float yawLimit = 70f;
        [SerializeField] float pitchMin = -50f;
        [SerializeField] float pitchMax = 40f;

        [Header("Cursor")]
        [SerializeField] bool lockCursorOnStart = true;

        float _yawOffset;
        float _pitch;
        Quaternion _initialYawRotation;

        void Awake()
        {
            if (yawPivot == null) yawPivot = transform;
            if (pitchPivot == null && yawPivot.childCount > 0) pitchPivot = yawPivot.GetChild(0);

            // 记录场景里摆好的初始朝向，之后所有旋转都是相对它的偏移，
            // 这样改机位只需要在 Scene 里转 Yaw，不用改代码里的数字。
            _initialYawRotation = yawPivot.localRotation;
        }

        void OnEnable()
        {
            if (lockCursorOnStart) SetCursorLocked(true);
        }

        void OnDisable()
        {
            SetCursorLocked(false);
        }

        void Update()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            // Esc 临时放出光标，方便在编辑器里中断游玩
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                SetCursorLocked(Cursor.lockState != CursorLockMode.Locked);

            // 光标没锁住时不接收视角输入，避免一边点编辑器一边转头
            if (Cursor.lockState != CursorLockMode.Locked) return;

            Vector2 delta = mouse.delta.ReadValue() * sensitivity;

            _yawOffset = Mathf.Clamp(_yawOffset + delta.x, -yawLimit, yawLimit);
            _pitch = Mathf.Clamp(_pitch + (invertY ? delta.y : -delta.y), pitchMin, pitchMax);

            yawPivot.localRotation = _initialYawRotation * Quaternion.Euler(0f, _yawOffset, 0f);
            if (pitchPivot != null)
                pitchPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        /// <summary>供剧情脚本调用：把视线复位到场景里的初始朝向。</summary>
        public void ResetView()
        {
            _yawOffset = 0f;
            _pitch = 0f;
            yawPivot.localRotation = _initialYawRotation;
            if (pitchPivot != null) pitchPivot.localRotation = Quaternion.identity;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            if (yawPivot == null) return;
            Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
            Vector3 p = yawPivot.position;
            float r = 2f;
            Gizmos.DrawLine(p, p + Quaternion.Euler(0f, -yawLimit, 0f) * yawPivot.forward * r);
            Gizmos.DrawLine(p, p + Quaternion.Euler(0f, yawLimit, 0f) * yawPivot.forward * r);
        }
#endif
    }
}
