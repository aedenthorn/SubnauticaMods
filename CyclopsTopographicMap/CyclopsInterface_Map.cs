using UnityEngine;
using static RootMotion.FinalIK.GenericPoser;

namespace CyclopsTopographicMap
{
    internal class CyclopsInterface_Map : MonoBehaviour
    {
        private void Awake()
        {
        }

        private void Start()
        {
            if (mapObject == null)
            {
                mapObject = Instantiate(interfacePrefab);
                mapObject.transform.SetParent(mapHolder.transform, false);
                mapObject.transform.localPosition = Vector3.zero;
                mapObject.transform.localScale = Vector3.one;
                mapObject.transform.position = mapSpawnPos.position;
                miniWorld = mapObject.GetComponentInChildren<MiniWorld>();
            }
        }

        private void Update()
        {
            if (Player.main is null || Player.main.GetPilotingChair() is null || Player.main.GetPilotingChair() != chair || !chair.subRoot.powerRelay.IsPowered())
            {
                miniWorld.active = false;
            }
            else if (GameInput.GetButtonDown(GameInput.Button.AltTool))
            {
                miniWorld.active = !miniWorld.active;
                BepInExPlugin.Dbgl($"Map active: {miniWorld.active}");
            }
            if (miniWorld.active)
            {
                mapHolder.transform.localPosition = BepInExPlugin.mapPosition.Value;
                playerDot?.SetActive(true);
                lightVfx?.SetActive(true);
            }
            else
            {
                playerDot?.SetActive(false);
                lightVfx?.SetActive(false);
            }
        }

        public CyclopsInterface_Map()
        {
        }

        [AssertNotNull]
        public GameObject interfacePrefab;

        [AssertNotNull]
        public GameObject mapHolder;

        [AssertNotNull]
        public Transform mapSpawnPos;

        [AssertNotNull]
        public GameObject playerDot;

        [AssertNotNull]
        public GameObject lightVfx;

        [AssertNotNull]
        public PilotingChair chair;


        private const int seaglideIllumMaterialIndex = 1;

        private MiniWorld miniWorld;

        private Color illumColor = Color.white;

        private GameObject mapObject;
    }
}