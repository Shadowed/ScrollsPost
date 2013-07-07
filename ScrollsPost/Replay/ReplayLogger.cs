using System;
using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Net;
using System.Text;
using JsonFx.Json;
using System.IO.Compression;

namespace ScrollsPost {
    public class ReplayLogger : IOkCancelCallback, ICommListener {
        private ScrollsPost.Mod mod;
        private String replayFolder;
        private String replayPath;
        private double lastMessage;
        private Boolean inGame;
        private Boolean enabled;

        public ReplayLogger(ScrollsPost.Mod mod) {
            this.mod = mod;
            App.Communicator.addListener(this);

            replayFolder = this.mod.OwnFolder() + Path.DirectorySeparatorChar + "replays";
            if( !Directory.Exists(replayFolder + Path.DirectorySeparatorChar) ) {
                Directory.CreateDirectory(replayFolder + Path.DirectorySeparatorChar);
            }
        }

        public void handleMessage(Message msg) {
            // Check if we should start recording
            if( msg is BattleRedirectMessage ) {
                enabled = true;
                return;
            // We left the game, ask if they want to upload it
            } else if( msg is LeaveGameMessage && !String.IsNullOrEmpty(replayPath) && mod.config.GetString("replay") == "ask" ) {
                App.Popups.ShowOkCancel(this, "replay", "Upload Replay?", "Do you want this replay to be uploaded to ScrollsPost.com?", "Yes", "No");
            // Not logging yet
            } else if( !enabled ) {
                return;
            }

            // Initial game start
            if( msg is GameInfoMessage ) {
                inGame = true;
                lastMessage = mod.TimeSinceEpoch();

                GameInfoMessage info = (GameInfoMessage) msg;

                Dictionary<String, object> metadata = new Dictionary<String, object>();
                metadata["perspective"] = info.color == TileColor.white ? "white" : "black";
                metadata["white-id"] = info.getPlayerProfileId(TileColor.white);
                metadata["black-id"] = info.getPlayerProfileId(TileColor.black);
                metadata["game-id"] = info.gameId.ToString();
                metadata["winner"] = "SPWINNERSP";
                metadata["played-at"] = (int) lastMessage;

                replayPath = replayFolder + Path.DirectorySeparatorChar + String.Format("{0}-{1}.spr", metadata["game-id"], metadata["perspective"]);

                // Store metadata for easier parsing
                using( StreamWriter sw = File.AppendText(replayPath) ) {
                    sw.WriteLine(String.Format("metadata|{0}", new JsonWriter().Write(metadata)));
                }

            // Junk we can ignore
            } else if( msg is BattleRejoinMessage || msg is FailMessage || msg is OkMessage ) {
                return;
            }

            if( !inGame )
                return;

            double epoch = mod.TimeSinceEpoch();
            using( StreamWriter sw = File.AppendText(replayPath) ) {
                sw.WriteLine(String.Format("elapsed|{0}|{1}", Math.Round(epoch - lastMessage, 2), msg.getRawText().Replace("\n", "")));
            }

            // Game over
            if( msg is NewEffectsMessage && msg.getRawText().Contains("EndGame") ) {
                inGame = false;
                enabled = false;

                // Bit of a hack, need to improve
                String contents = File.ReadAllText(replayPath);
                contents.Replace("SPWINNERSP", msg.getRawText().Contains("winner\":\"white\"") ? "white" : "black");
                File.WriteAllText(replayPath, contents);

                if( mod.config.GetString("replay") == "auto" ) {
                    new Thread(new ThreadStart(Upload));
                }
            }

            lastMessage = epoch;
        }

        public void onReconnect() {
            return;
        }

        // Handle replay uploading
        public void PopupCancel(String type) {
            mod.SendMessage("Replay will not be uploaded, you can always manually upload it later if you change your mind.");
        }

        public void PopupOk(String type) {
            mod.SendMessage("Replay is being uploaded...");
            new Thread(new ThreadStart(Upload));
        }

        private void Upload() {
            try {
                WebClient wc = new WebClient();
                String contents = Encoding.ASCII.GetString(wc.UploadFile(new Uri(mod.apiURL + "/v1/replays"), replayPath));
                Dictionary<String, object> response = new JsonReader().Read<Dictionary<String, object>>(contents);

                if( response.ContainsKey("url") ) {
                    mod.SendMessage(String.Format("Finished uploading replay to ScrollsPost. Can be found at {0}", response["url"]));
                } else {
                    mod.SendMessage(String.Format("Error while uploading replay ({0}), please contact us for more info at support@scrollspost.com", response["error"]));
                }

            } catch ( WebException we ) {
                Console.WriteLine("**** ERROR {0}", we.ToString());
                mod.SendMessage(String.Format("We had an HTTP error while uploading replay {0}, contact us at support@scrollspost.com for help.", Path.GetFileName(replayPath)));
                mod.WriteLog("Failed to sync collection", we);
            }
        }
    }
}

