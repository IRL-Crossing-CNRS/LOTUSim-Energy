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
//  GameManager.cs
//
//  Description:
//  Handles player and camera instantiation depending on the user type (player or spectator).
//  Integrates Photon networking for spawning and room management.
// --------------------------------------------------------------------------------------------------------------------


using System;
using System.Collections;

using UnityEngine;
using UnityEngine.SceneManagement;

using Photon.Pun;
using Photon.Realtime;

namespace Com.MyCompany.MyGame
{
	#pragma warning disable 649

	/// <summary>
	/// Game manager.
	/// Connects and watch Photon Status, Instantiate Player
	/// Deals with quiting the room and the game
	/// Deals with level loading (outside the in room synchronization)
	/// </summary>
	public class GameManager : MonoBehaviourPunCallbacks
    {

		#region Public Fields

		static public GameManager Instance;
		//public GameObject playerCamera; // The regular player camera
		public GameObject spectatorCameraRobotPrefab; // Reference to the SpectatorCameraRobot prefab

		#endregion


		#region Private Fields

		private GameObject instance;

		private GameObject spectatorCameraInstance; // The instantiated spectator camera

        #endregion


		#region Private Serializable Fields

		[Tooltip("The prefab to use for representing the player")]
        [SerializeField]
        private GameObject playerPrefab;

		#endregion


        #region MonoBehaviour CallBacks

        /// <summary>
        /// MonoBehaviour method called on GameObject by Unity during initialization phase.
        /// </summary>
        void Start()
		{
			Instance = this;

			//if (PlayerManager.LocalPlayerInstance != null)
			//{
			//	Debug.Log("Player already exists, skipping instantiation.");
			//	return;
			//}

			// in case we started this demo with the wrong scene being active, simply load the menu scene
			if (!PhotonNetwork.IsConnected)
			{
				SceneManager.LoadScene("Launcher");

				return;
			}

			if (playerPrefab == null) { // #Tip Never assume public properties of Components are filled up properly, always check and inform the developer of it.

				Debug.LogError("<Color=Red><b>Missing</b></Color> playerPrefab Reference. Please set it up in GameObject 'Game Manager'", this);
			} else {


				if (PhotonNetwork.InRoom && PlayerManager.LocalPlayerInstance == null && !Launcher.IsSpectator)
				{
				    Debug.LogFormat("We are Instantiating LocalPlayer from {0} (start)", SceneManagerHelper.ActiveSceneName);

					// we're in a room. spawn a character for the local player. it gets synced by using PhotonNetwork.Instantiate
					//PhotonNetwork.Instantiate(this.playerPrefab.name, new Vector3(0f,5f,0f), Quaternion.identity, 0);

					// ------------ Set the spawning position of the player -------------------------------------------------------------------------------------------
					// GameObject player = PhotonNetwork.Instantiate("Multi User/" + this.playerPrefab.name, new Vector3(-3000f, 40f, -3000f), Quaternion.identity, 0);
					// Debug.Log("Start() : Spawning player at: " + new Vector3(-3000f, 40f, -3000f));
					// ------------- Don't forget to also set the same position to the MainCamera in Unity associated to the player -----------------------------------

					GameObject player = PhotonNetwork.Instantiate("Multi User/" + this.playerPrefab.name, new Vector3(-3005, 40f, 5f), Quaternion.identity, 0);
					Debug.Log("Start() : Spawning player at: " + new Vector3(-3000f, 40f, -3000f));

					if (spectatorCameraInstance != null)
					{
						Destroy(spectatorCameraInstance);
						spectatorCameraInstance = null;
					}

					// Call SetupCamera with isSpectator = false, since we just instantiated a player 
					Debug.LogFormat("Setting up the camera ...");
					SetupCamera(false);
					Debug.LogFormat("Call SetupCamera with isSpectator = false, since we just instantiated a player");

				}
				else
				{
					// just in case player is already instantiated or it's a spectator
            		SetupCamera(Launcher.IsSpectator);
					Debug.LogFormat("isSpectator= {0}", Launcher.IsSpectator);
					Debug.LogFormat("Ignoring scene load for {0}", SceneManagerHelper.ActiveSceneName);
					Debug.LogFormat("set up my camera depending on whether the player is a spectator or not!");
				}
			}
		}

		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity on every frame.
		/// </summary>
		void Update()
		{
			// "back" button of phone equals "Escape". quit app if that's pressed
			// if (Input.GetKeyDown(KeyCode.Escape))
			// {
			// 	QuitApplication();
			// }
		}

        #endregion

        #region Photon Callbacks


		public override void OnJoinedRoom()
		{
			// Note: it is possible that this monobehaviour is not created (or active) when OnJoinedRoom happens
			// due to that the Start() method also checks if the local player character was network instantiated!
			if (PlayerManager.LocalPlayerInstance == null && !Launcher.IsSpectator)
			{
				Debug.LogFormat("We are Instantiating LocalPlayer from {0} (OnJoinedRoom)", SceneManagerHelper.ActiveSceneName);

				// We're in a room. Spawn a character for the local player. It gets synced by using PhotonNetwork.Instantiate
				GameObject player = PhotonNetwork.Instantiate("Multi User/" + this.playerPrefab.name, new Vector3(-3000f, 40f, -3000f), Quaternion.identity, 0);
				Debug.Log("OnJoinedRoom() : Spawning player at: " + new Vector3(-3000f, 40f, -3000f));

				if (spectatorCameraInstance != null)
				{
					Destroy(spectatorCameraInstance);
					spectatorCameraInstance = null;
				}

				// Call SetupCamera with isSpectator = false, since we just instantiated a player
				SetupCamera(false);
			}
			else
			{
				// If the player prefab was already instantiated, check if we need to setup the spectator camera
				SetupCamera(true);
			}
		}


		/// <summary>
		/// Called when the local player left the room. We need to load the launcher scene.
		/// </summary>
		public override void OnLeftRoom()
		{
			Debug.Log("Loading Launcher...");
			SceneManager.LoadScene("Launcher");
		}

		#endregion

		#region Public Methods

		public void LeaveRoom() 
		{
			Debug.Log("LeaveRoom called.");
			Debug.Log("In room? " + PhotonNetwork.InRoom);
			PhotonNetwork.LeaveRoom();
		}

		public void QuitApplication()
		{
			Application.Quit();
		}


		#endregion
		

		#region Private Methods

		private void SetupCamera(bool isSpectator)
		{
			Camera targetCamera = null;

			if (isSpectator)
			{
				GameObject spectatorCamObj = GameObject.Find("SpectatorCameraRobot");
				if (spectatorCamObj != null)
				{
					targetCamera = spectatorCamObj.GetComponentInChildren<Camera>();
				}
			}
			else
			{
				targetCamera = Camera.main;
			}

			if (targetCamera != null)
			{
				// First, disable all other cameras (except the one we want to use)
				foreach (Camera cam in Camera.allCameras)
				{
					if (cam != targetCamera)
					{
						cam.gameObject.SetActive(false);
					}
				}

				// Then activate the target camera
				targetCamera.gameObject.SetActive(true);

				// Make sure the target camera is tagged MainCamera if not already
				if (!targetCamera.CompareTag("MainCamera"))
				{
					// Clear other MainCamera tags first
					foreach (Camera cam in Camera.allCameras)
					{
						if (cam.CompareTag("MainCamera"))
						{
							cam.tag = "Untagged";
						}
					}

					targetCamera.tag = "MainCamera";
				}
			}
			else
			{
				Debug.LogError("SetupCamera: No target camera found!");
			}
		}

		#endregion

	}	
}


