// // --------------------------------------------------------------------------------------------------------------------
// //  Used to connect, and join/create room automatically on player or spectator mode
// // --------------------------------------------------------------------------------------------------------------------

// using UnityEngine;
// using UnityEngine.UI;

// using Photon.Realtime;

// using Photon.Pun;
// using TMPro;


// namespace Com.MyCompany.MyGame
// {
// 	#pragma warning disable 649

//     /// <summary>
//     /// Launch manager. Connect, join a random room or create one if none or all full.
//     /// </summary>
// 	public class Launcher : MonoBehaviourPunCallbacks
//     {

// 		#region Public Fields

// 		public static bool IsSpectator = false;

// 		#endregion


// 		#region Private Serializable Fields

// 		[Tooltip("The Ui Panel to let the user enter name, connect and play")]
// 		[SerializeField]
// 		private GameObject controlPanel;

// 		[Tooltip("The Ui Text to inform the user about the connection progress")]
// 		[SerializeField]
// 		//private Text feedbackText;
// 		private TMP_Text feedbackText;

// 		[Tooltip("The maximum number of players per room")]
// 		[SerializeField]
// 		private byte maxPlayersPerRoom = 20;

// 		[Tooltip("The UI Loader Anime")]
// 		[SerializeField]
// 		private LoaderAnime loaderAnime;

// 		[SerializeField]
// 		private Toggle spectatorToggle;

// 		#endregion

// 		#region Private Fields
// 		/// <summary>
// 		/// Keep track of the current process. Since connection is asynchronous and is based on several callbacks from Photon, 
// 		/// we need to keep track of this to properly adjust the behavior when we receive call back by Photon.
// 		/// Typically this is used for the OnConnectedToMaster() callback.
// 		/// </summary>
// 		bool isConnecting;

// 		/// <summary>
// 		/// This client's version number. Users are separated from each other by gameVersion (which allows you to make breaking changes).
// 		/// </summary>
// 		string gameVersion = "1";

// 		#endregion

// 		#region MonoBehaviour CallBacks

// 		/// <summary>
// 		/// MonoBehaviour method called on GameObject by Unity during early initialization phase.
// 		/// </summary>
// 		void Awake()
// 		{
// 			if (loaderAnime==null)
// 			{
// 				Debug.LogError("<Color=Red><b>Missing</b></Color> loaderAnime Reference.",this);
// 			}
// 			Debug.Log("Launcher Awake");
// 			// #Critical
// 			// this makes sure we can use PhotonNetwork.LoadLevel() on the master client and all clients in the same room sync their level automatically
// 			PhotonNetwork.AutomaticallySyncScene = true;

// 		}

// 		#endregion


// 		#region Public Methods

// 		/// <summary>
// 		/// Start the connection process. 
// 		/// - If already connected, we attempt joining a random room
// 		/// - if not yet connected, Connect this application instance to Photon Cloud Network
// 		/// </summary>
// 		public void Connect()
// 		{
// 			// we want to make sure the log is clear everytime we connect, we might have several failed attempted if connection failed.
// 			feedbackText.text = "";

// 			// keep track of the will to join a room, because when we come back from the game we will get a callback that we are connected, so we need to know what to do then
// 			isConnecting = true;

// 			// hide the Play button for visual consistency
// 			controlPanel.SetActive(false);

// 			// start the loader animation for visual effect.
// 			if (loaderAnime!=null)
// 			{
// 				loaderAnime.StartLoaderAnimation();
// 			}

// 			// Set spectator mode and nickname before connecting
// 			if (spectatorToggle != null)
// 			{
// 				IsSpectator = spectatorToggle.isOn;
// 			}
// 			else
// 			{
// 				Debug.LogError("spectatorToggle is not assigned in the Inspector! Please assign it via the Inspector.");
// 			}
			
// 			/// <summary>
// 			/// Set the name of the players/spectators using the value imput by the user in PlayerNameInputField.cs
// 			/// </summary>
// 			string rolePrefix = IsSpectator ? "Spectator_" : "Player_";
// 			// Try to get saved player name from PlayerPrefs
// 			string savedName = PlayerPrefs.HasKey("PlayerName") ? PlayerPrefs.GetString("PlayerName") : null;

// 			if (!string.IsNullOrEmpty(savedName))
// 			{
// 				PhotonNetwork.NickName = savedName;
// 			}
// 			else
// 			{
// 				PhotonNetwork.NickName = rolePrefix + UnityEngine.Random.Range(1000, 9999);
// 			}
// 			Debug.Log("Connecting as: " + PhotonNetwork.NickName);


// 			// we check if we are connected or not, we join if we are , else we initiate the connection to the server.
// 			if (PhotonNetwork.IsConnected)
// 			{
// 				LogFeedback("Joining Room...");
// 				// #Critical we need at this point to attempt joining a Random Room. If it fails, we'll get notified in OnJoinRandomFailed() and we'll create one.
// 				//PhotonNetwork.JoinRandomRoom();
// 				PhotonNetwork.JoinOrCreateRoom("SilentStormWithRos", new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);

// 			}else{

// 				LogFeedback("Connecting...");
				
// 				// #Critical, we must first and foremost connect to Photon Online Server.
// 				PhotonNetwork.ConnectUsingSettings();
//                 PhotonNetwork.GameVersion = this.gameVersion;
// 			}
// 		}

// 		/// <summary>
// 		/// Logs the feedback in the UI view for the player, as opposed to inside the Unity Editor for the developer.
// 		/// </summary>
// 		/// <param name="message">Message.</param>
// 		void LogFeedback(string message)
// 		{
// 			// we do not assume there is a feedbackText defined.
// 			if (feedbackText == null) {
// 				return;
// 			}

// 			// add new messages as a new line and at the bottom of the log.
// 			feedbackText.text += System.Environment.NewLine+message;
// 		}

//         #endregion


//         #region MonoBehaviourPunCallbacks CallBacks
//         // below, we implement some callbacks of PUN
//         // you can find PUN's callbacks in the class MonoBehaviourPunCallbacks


//         /// <summary>
//         /// Called after the connection to the master is established and authenticated
//         /// </summary>
//         public override void OnConnectedToMaster()
// 		{
//             // we don't want to do anything if we are not attempting to join a room. 
// 			// this case where isConnecting is false is typically when you lost or quit the game, when this level is loaded, OnConnectedToMaster will be called, in that case
// 			// we don't want to do anything.
// 			if (isConnecting)
// 			{
// 				LogFeedback("OnConnectedToMaster: Next -> try to Join SilentStormWithRos");
// 				Debug.Log("Trying to join or create 'SilentStormWithRos'");
// 				PhotonNetwork.JoinOrCreateRoom("SilentStormWithRos", new RoomOptions { MaxPlayers = this.maxPlayersPerRoom }, TypedLobby.Default);
// 				// #Critical: The first we try to do is to join a potential existing room. If there is, good, else, we'll be called back with OnJoinRandomFailed()
// 				//PhotonNetwork.JoinRandomRoom();
				

// 			}
// 		}

// 		/// <summary>
// 		/// Called when a JoinRandom() call failed. The parameter provides ErrorCode and message.
// 		/// </summary>
// 		/// <remarks>
// 		/// Most likely all rooms are full or no rooms are available. <br/>
// 		/// </remarks>
// 		public override void OnJoinRandomFailed(short returnCode, string message)
// 		{
// 			LogFeedback("<Color=Red>OnJoinRandomFailed</Color>: Next -> Create a new Room");
// 			Debug.Log("OnJoinRandomFailed() was called by PUN. No random room available, so we create one.\nCalling: PhotonNetwork.CreateRoom");

// 			// #Critical: we failed to join a random room, maybe none exists or they are all full. No worries, we create a new room.
// 			PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = this.maxPlayersPerRoom});
// 		}


// 		/// <summary>
// 		/// Called after disconnecting from the Photon server.
// 		/// </summary>
// 		public override void OnDisconnected(DisconnectCause cause)
// 		{
// 			LogFeedback("<Color=Red>OnDisconnected</Color> "+cause);
// 			Debug.LogError("Launcher:Disconnected");

// 			// #Critical: we failed to connect or got disconnected. There is not much we can do. Typically, a UI system should be in place to let the user attemp to connect again.
// 			loaderAnime.StopLoaderAnimation();

// 			isConnecting = false;
// 			controlPanel.SetActive(true);

// 		}

// 		/// <summary>
// 		/// Called when entering a room (by creating or joining it). Called on all clients (including the Master Client).
// 		/// </summary>
// 		/// <remarks>
// 		/// This method is commonly used to instantiate player characters.
// 		/// If a match has to be started "actively", you can call an [PunRPC](@ref PhotonView.RPC) triggered by a user's button-press or a timer.
// 		///
// 		/// When this is called, you can usually already access the existing players in the room via PhotonNetwork.PlayerList.
// 		/// Also, all custom properties should be already available as Room.customProperties. Check Room..PlayerCount to find out if
// 		/// enough players are in the room to start playing.
// 		/// </remarks>
// 		public override void OnJoinedRoom()
// 		{
// 			LogFeedback("<Color=Green>OnJoinedRoom</Color> with "+PhotonNetwork.CurrentRoom.PlayerCount+" Player(s)");
// 			Debug.Log("Launcher: OnJoinedRoom() called by PUN. Now this client is in a room.\nFrom here on, your game would be running.");
		
// 			// #Critical: We only load if we are the first player, else we rely on  PhotonNetwork.AutomaticallySyncScene to sync our instance scene.

// 			Debug.Log("We load the 'SilentStormWithRos' ");

// 			// #Critical
// 			// Load the Room Level. 
// 			PhotonNetwork.LoadLevel("SilentStormWithRos");

			
// 		}

// 		#endregion
		
// 	}
// }