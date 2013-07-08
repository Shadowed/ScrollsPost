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
        public String replayFolder;
        private String replayPath;
        private String currentVersion;
        private double lastMessage;
        private Boolean inGame;
        private Boolean enabled;
        private StreamWriter sw;

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
            } else if( msg is ServerInfoMessage ) {
                currentVersion = (msg as ServerInfoMessage).version;

            } else if( enabled && msg is ProfileInfoMessage ) {
                if( mod.config.GetString("replay").Equals("ask") ) {
                    App.Popups.ShowOkCancel(this, "replay", "Upload Replay?", "Do you want this replay to be uploaded to ScrollsPost.com?", "Yes", "No");
                }

                enabled = false;

            // Not logging yet
            } else if( !enabled ) {
                return;
            }

            // Initial game start
            if( msg is GameInfoMessage ) {
                if( inGame )
                    return;

                inGame = true;
                lastMessage = mod.TimeSinceEpoch();

                GameInfoMessage info = (GameInfoMessage) msg;

                Dictionary<String, object> metadata = new Dictionary<String, object>();
                metadata["perspective"] = info.color == TileColor.white ? "white" : "black";
                metadata["white-id"] = info.getPlayerProfileId(TileColor.white);
                metadata["black-id"] = info.getPlayerProfileId(TileColor.black);
                metadata["white-name"] = info.getPlayerName(TileColor.white);
                metadata["black-name"] = info.getPlayerName(TileColor.black);
                metadata["deck"] = info.deck;
                metadata["game-id"] = info.gameId.ToString();
                metadata["winner"] = "SPWINNERSP";
                metadata["played-at"] = (int) lastMessage;
                metadata["version"] = currentVersion;

                replayPath = replayFolder + Path.DirectorySeparatorChar + String.Format("{0}-{1}.spr", metadata["game-id"], metadata["perspective"]);

                // Store metadata for easier parsing
                int buffer = mod.config.ContainsKey("buffer") ? mod.config.GetInt("buffer") : 4096;
                sw = new StreamWriter(replayPath, true, Encoding.UTF8, buffer);
                sw.WriteLine(String.Format("metadata|{0}", new JsonWriter().Write(metadata)));

            // Junk we can ignore
            } else if( msg is BattleRejoinMessage || msg is FailMessage || msg is OkMessage ) {
                return;
            }

            if( !inGame )
                return;

            double epoch = mod.TimeSinceEpoch();
            sw.WriteLine(String.Format("elapsed|{0}|{1}", Math.Round(epoch - lastMessage, 2), msg.getRawText().Replace("\n", "")));
        
            // Game over
            if( msg is NewEffectsMessage && msg.getRawText().Contains("EndGame") ) {
                inGame = false;

                // Finish off
                sw.Flush();
                sw.Close();
                sw = null;

                // Bit of a hack, need to improve somehow
                String contents = File.ReadAllText(replayPath);
                contents = contents.Replace("SPWINNERSP", msg.getRawText().Contains("winner\":\"white\"") ? "white" : "black");
                File.WriteAllText(replayPath, contents);

                // Start uploading immediately since we don't need to wait for anyone
                if( mod.config.GetString("replay").Equals("auto") ) {
                    new Thread(new ThreadStart(Upload)).Start();
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
            new Thread(new ThreadStart(Upload)).Start();
        }

        private void Upload() {
            // Setup
            String boundary = String.Format("---------------------------{0}", (int)mod.TimeSinceEpoch());
            byte[] boundaryBytes = Encoding.ASCII.GetBytes(String.Format("\r\n--{0}\r\n", boundary));

            HttpWebRequest wr = (HttpWebRequest) WebRequest.Create(mod.apiURL + "/v1/replays");
            wr.Method = "POST";
            wr.ContentType = String.Format("multipart/form-data; boundary={0}", boundary);

            // Start the boundary off
            using( Stream stream = wr.GetRequestStream() ) {
                stream.Write(boundaryBytes, 0, boundaryBytes.Length);

                // File info
                String field = String.Format("Content-Disposition: form-data; name=\"{0}\"; filename=\"{1}\"\r\nContent-Type: {2}\r\n\r\n", "replay", Path.GetFileName(replayPath), "text/plain");
                byte[] bytes = Encoding.UTF8.GetBytes(field);

                stream.Write(bytes, 0, bytes.Length);

                // Write the file
                bytes = File.ReadAllBytes(replayPath);
                stream.Write(bytes, 0, bytes.Length);

                bytes = Encoding.ASCII.GetBytes(String.Format("\r\n--{0}--\r\n", boundary));
                stream.Write(bytes, 0, bytes.Length);
            }

            try {
                using( WebResponse wres = wr.GetResponse() ) {
                    using( StreamReader rs = new StreamReader(wres.GetResponseStream()) ) {
                        String contents = rs.ReadToEnd();
                        Dictionary<String, object> response = new JsonReader().Read<Dictionary<String, object>>(contents);

                        if( response.ContainsKey("url") ) {
                            mod.SendMessage(String.Format("Finished uploading replay to ScrollsPost. Can be found at {0}", response["url"]));
                        } else {
                            mod.SendMessage(String.Format("Error while uploading replay ({0}), please contact us for more info at support@scrollspost.com", response["error"]));
                        }
                    }
                }

            } catch ( WebException we ) {
                Console.WriteLine("**** ERROR {0}", we.ToString());
                mod.SendMessage(String.Format("We had an HTTP error while uploading replay {0}, contact us at support@scrollspost.com for help.", Path.GetFileName(replayPath)));
                mod.WriteLog("Failed to sync collection", we);
            }
        }
    }
}

