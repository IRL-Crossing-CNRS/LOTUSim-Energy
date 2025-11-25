/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */

// --------------------------------------------------------------------------------------------------------------------
//  LotusimConnector.cs
//
//  Description:
//  Main Unity interface for Lotusim. Wraps LotusimBaseInterface implementations (ROS2, TCPIP)
//  Handles creation, destruction, and updating of vessels, transforms, and animations.
// --------------------------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace Lotusim
{
    /// <summary>
    /// LotusimInterface is the main class in Unity interfacing with Lotusim.
    /// It wraps around different interfaces and handles all objects and transforms,
    /// while LotusimBaseInterface focuses on handling the interface.
    /// </summary>
    ///
    /// TBC: Control Unity time to sync with Gazebo. Allow the waves to sync
    public class LotusimInterface : MonoBehaviour
    {
        [Header("Interface Settings")]
        [SerializeField] private string m_namespace = "Silent_Storm";
        [SerializeField] private string selectedInterfaceType = "ROS2";

        private LotusimBaseInterface m_interface;

        // Mapping of GameObject name to transformation.
        private Dictionary<string, Transform> m_transformMap = new Dictionary<string, Transform>();

        // Mapping of GameObject name to GameObject.
        private Dictionary<string, GameObject> m_objectMap = new Dictionary<string, GameObject>();

        // Mapping of Animation name to Animation.
        private Dictionary<string, Animator> m_animatorMap = new Dictionary<string, Animator>();

        public void OnNamespaceChanged()
        {
            Debug.Log("Handling namespace change: " + m_namespace);
            if (m_interface != null)
            {
                m_interface.Destroy();
            }
            m_interface.Start(m_namespace);
        }

        public void OnInterfaceTypeChanged()
        {
            Debug.Log("Handling interface type change");
            if (m_interface != null)
            {
                m_interface.Destroy();
            }
            Start();
        }

        private void Start()
        {
            m_interface = LotusimInterfaceFactory.CreateInterface(selectedInterfaceType);
            UpdateVesselsList();
            m_interface.Start(m_namespace);
        }

        private void Update()
        {
            m_interface.Update();
            UpdateVesselPoses();
            ProcessCommands();
        }

        private void ProcessCommands()
        {
            ProcessCreateCmds();
            ProcessExplodeCmds();
            ProcessDestroyCmds();
            ProcessPropellerSpin();
        }

        private void ProcessCreateCmds()
        {
            foreach (var kvp in m_interface.m_vesselToCreate)
            {
                string vesselName = kvp.Key;
                string assetAddress = kvp.Value.Item1;
                Pose pose = kvp.Value.Item2;

                if (m_objectMap.ContainsKey(vesselName))
                {
                    continue;
                }

                Addressables.LoadAssetAsync<GameObject>(assetAddress).Completed += handle =>
                    OnFindAssetLocation(handle, vesselName, assetAddress, pose);
            }
            m_interface.m_vesselToCreate.Clear();
        }

        void OnFindAssetLocation(
            AsyncOperationHandle<GameObject> handle,
            string vesselName,
            string assetAddress,
            Pose pose
        )
        {
            if (handle.Status == AsyncOperationStatus.Succeeded)
            {
                // Instantiate the prefab at the origin with no rotation
                GameObject instantiatedObject = Instantiate(
                    handle.Result,
                    pose.position,
                    pose.rotation
                );
                instantiatedObject.name = vesselName;

                m_objectMap.Add(vesselName, instantiatedObject);
                m_transformMap.Add(vesselName, instantiatedObject.transform);

                Animator[] _animator = instantiatedObject.GetComponentsInChildren<Animator>();
                foreach (Animator _a in _animator)
                {
                    string thruster_namespace = instantiatedObject.name + "/" + _a.name;
                    m_animatorMap.Add(thruster_namespace, _a);
                    Debug.Log("Thruster found: " + thruster_namespace);
                }
            }
            else
            {
                Debug.LogWarning($"Failed to create object with type {assetAddress}.");
            }
        }

        private void ProcessDestroyCmds()
        {
            foreach (var vessel_name in m_interface.m_vesselsToDestroy)
            {
                DestroyObject(vessel_name);
            }
            m_interface.m_vesselsToDestroy.Clear();
        }

        private void DestroyObject(string vessel_name)
        {
            if (m_objectMap.ContainsKey(vessel_name))
            {
                Animator[] _animator = m_objectMap[vessel_name].GetComponentsInChildren<Animator>();
                foreach (Animator _a in _animator)
                {
                    string thruster_namespace = vessel_name + "/" + _a.name;
                    m_animatorMap.Remove(thruster_namespace);
                    Debug.Log("Thruster removed: " + thruster_namespace);
                }
                Destroy(m_objectMap[vessel_name]);
                m_objectMap.Remove(vessel_name);
                m_transformMap.Remove(vessel_name);
            }
        }

        // TODO: add angle explosions for velocity objects
        private void ProcessExplodeCmds()
        {
            foreach (var vessel_name in m_interface.m_vesselsToExplode)
            {
                if (m_objectMap.ContainsKey(vessel_name))
                {
                    Vector3 position = m_objectMap[vessel_name].transform.position;
                    Quaternion rotation = m_objectMap[vessel_name].transform.rotation;

                    Addressables.LoadAssetAsync<GameObject>("explosion_area").Completed += handle =>
                    {
                        if (handle.Status == AsyncOperationStatus.Succeeded)
                        {
                            GameObject instantiatedObject = Instantiate(
                                handle.Result,
                                position,
                                rotation
                            );
                        }
                        else
                        {
                            Debug.LogWarning("Failed to create explosion.");
                        }
                    };

                    DestroyObject(vessel_name);
                }
            }
            m_interface.m_vesselsToExplode.Clear();
        }

        private void ProcessPropellerSpin()
        {
            foreach (var (animator_name, spin_ratio) in m_interface.m_propellerSpinRatios)
            {
                if (m_animatorMap.ContainsKey(animator_name))
                {
                    m_animatorMap[animator_name].speed = spin_ratio;
                }
            }
            m_interface.m_propellerSpinRatios.Clear();
        }

        private void OnDestroy()
        {
            m_interface.Destroy();
        }

        private void UpdateVesselsList()
        {
            m_transformMap.Clear();
            m_objectMap.Clear();

            var allGameObjects = FindObjectsOfType<GameObject>(true);
            var rootObjects = allGameObjects
                .Where(go => go != null && go.transform.parent == null)
                .ToArray();

            foreach (var gameObject in rootObjects)
            {
                m_transformMap[gameObject.name] = gameObject.transform;
                m_objectMap[gameObject.name] = gameObject;
                Animator[] _animator = gameObject.GetComponentsInChildren<Animator>();
                foreach (Animator _a in _animator)
                {
                    string animator_name_with_namespace = gameObject.name + "/" + _a.name;
                    // m_animatorMap.Add(animator_name_with_namespace, _a);
                }
            }
        }

        private void UpdateVesselPoses()
        {
            float interpolationRatio = m_interface.GetInterpolationRatio();
            if (interpolationRatio > 0)
            {
                Time.timeScale = interpolationRatio;
                foreach (var (vessel_name, pose) in m_interface.m_vesselPoses)
                {
                    if (!m_transformMap.TryGetValue(vessel_name, out var transform))
                    {
                        UpdateVesselsList();
                        continue;
                    }
                    Pose poseUnityFrame = CoordinateSystemUtils.GzPoseToUnityPose(pose);
                    InterpolateVesselPosition(transform, poseUnityFrame, interpolationRatio);
                }
            }
            else
            {
                Time.timeScale = 0;
            }
            m_interface.m_vesselPoses.Clear();
        }

        private void InterpolateVesselPosition(
            Transform transform,
            Pose poseUnityFrame,
            float interpolationRatio
        )
        {
            Vector3 interpolatedPosition = Vector3.Lerp(
                transform.position,
                poseUnityFrame.position,
                interpolationRatio
            );

            Quaternion interpolatedRotation = Quaternion.Lerp(
                transform.rotation,
                poseUnityFrame.rotation,
                interpolationRatio
            );

            transform.SetPositionAndRotation(interpolatedPosition, interpolatedRotation);
        }
    }
}
