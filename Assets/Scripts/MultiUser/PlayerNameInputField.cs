/*
 * Copyright (c) 2025 Naval Group
 *
 * This program and the accompanying materials are made available under the
 * terms of the Eclipse Public License 2.0 which is available at
 * https://www.eclipse.org/legal/epl-2.0.
 *
 * SPDX-License-Identifier: EPL-2.0
 */
﻿// --------------------------------------------------------------------------------------------------------------------
//  PlayerNameInputField.cs
//
//  Description:
//  Let the player input their name to be saved as the network player name.
//  Displayed above the player when in the same room.
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.UI;
using TMPro; // Use TextMeshPro instead of UI
using Photon.Pun;


namespace Com.MyCompany.MyGame
{
	/// <summary>
	/// Player name input field. Let the user input his name, will appear above the player in the game.
	/// </summary>
	//[RequireComponent(typeof(InputField))]
	[RequireComponent(typeof(TMP_InputField))]
	public class PlayerNameInputField : MonoBehaviour
	{
		#region Private Constants

		// Store the PlayerPref Key to avoid typos
		const string playerNamePrefKey = "PlayerName";

		#endregion

		#region MonoBehaviour CallBacks
		
		/// <summary>
		/// MonoBehaviour method called on GameObject by Unity during initialization phase.
		/// </summary>
		void Start () {
		
			string defaultName = string.Empty;
			InputField _inputField = this.GetComponent<InputField>();

			if (_inputField!=null)
			{
				if (PlayerPrefs.HasKey(playerNamePrefKey))
				{
					defaultName = PlayerPrefs.GetString(playerNamePrefKey);
					_inputField.text = defaultName;
				}
			}

			PhotonNetwork.NickName =	defaultName;
		}

		#endregion
		
		#region Public Methods

		/// <summary>
		/// Sets the name of the player, and save it in the PlayerPrefs for future sessions.
		/// </summary>
		/// <param name="value">The name of the Player</param>
		public void SetPlayerName(string value)
		{
			// #Important
		    if (string.IsNullOrEmpty(value))
		    {
                Debug.LogError("Player Name is null or empty");
		        return;
		    }
			PhotonNetwork.NickName = value;

			PlayerPrefs.SetString(playerNamePrefKey, value);
		}
		
		#endregion
	}
}
