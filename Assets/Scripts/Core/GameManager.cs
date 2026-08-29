using IndieGame.Persistence;
using UnityEngine;

namespace IndieGame.Core
{
    /// <summary>
    /// Scene-level setup. Its main job is the physics timestep: a stiff tyre slip
    /// curve overshoots inside Unity's default 50 Hz step and oscillates, so the
    /// simulation is run at 200 Hz. Setting it here rather than only in
    /// ProjectSettings means the scene behaves correctly even in a project whose
    /// settings were never configured.
    /// </summary>
    [DefaultExecutionOrder(-500)]
    [AddComponentMenu("IndieGame/Core/Game Manager")]
    public class GameManager : MonoBehaviour
    {
        [Tooltip("Physics step in seconds. 0.005 is 200 Hz.")]
        [SerializeField] private float fixedTimestep = 0.005f;

        [Tooltip("Upper bound on how much simulation one long frame may catch up on.")]
        [SerializeField] private float maximumDeltaTime = 0.06f;

        [SerializeField] private int solverIterations = 12;
        [SerializeField] private int solverVelocityIterations = 6;

        [Tooltip("Target frame rate. -1 leaves it to the platform and VSync.")]
        [SerializeField] private int targetFrameRate = -1;

        [Tooltip("Write the save file when the application quits.")]
        [SerializeField] private bool saveOnQuit = true;

        public static GameManager Instance { get; private set; }

        private void Awake()
        {
            Instance = this;

            Time.fixedDeltaTime = Mathf.Clamp(fixedTimestep, 0.001f, 0.02f);
            Time.maximumDeltaTime = Mathf.Max(Time.fixedDeltaTime * 4f, maximumDeltaTime);

            Physics.defaultSolverIterations = Mathf.Clamp(solverIterations, 4, 32);
            Physics.defaultSolverVelocityIterations = Mathf.Clamp(solverVelocityIterations, 1, 16);

            if (targetFrameRate > 0) Application.targetFrameRate = targetFrameRate;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnApplicationQuit()
        {
            if (saveOnQuit) SaveSystem.Save();
        }
    }
}
