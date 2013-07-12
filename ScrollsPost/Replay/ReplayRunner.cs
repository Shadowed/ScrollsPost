using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using JsonFx.Json;
using ScrollsModLoader.Interfaces;
using UnityEngine;

namespace ScrollsPost {
    public class ReplayRunner : IOkStringCancelCallback, IOkCancelCallback {
        //private ScrollsPost.Mod mod;
        private String replayPrimaryPath;
        private String replaySecondaryPath;
        private String primaryType;
        private Boolean msgPending = false;
        private Thread playerThread;
        private Dictionary<String, object> metadata;

        private Boolean sceneLoaded = false;
        private Boolean finished = false;
        private Boolean realtime = false;
        private Boolean paused = false;
        private Boolean wasPaused = false;
        private Boolean internalPause = false;
        private Boolean rewind = false;
        private int seekRound = 0;
        private float speed = 1;

        private GUIStyle buttonStyle;
        private GUIStyle speedButtonStyle;
        private GUIStyle realTimeButtonStyle;

        private MethodInfo deselectMethod;
        private FieldInfo effectField;
        //private FieldInfo speedField;
        private FieldInfo animFrameField;

        public ReplayRunner(ScrollsPost.Mod mod, String path) {
            //this.mod = mod;
            this.replayPrimaryPath = path;
            this.primaryType = path.Contains("-white.spr") ? "white" : "black";

            // Check if we have both perspectives available to us
            String secondary = path.Contains("-white.spr") ? path.Replace("-white.spr", "-black.spr") : path.Replace("-black.spr", "-white.spr");
            if( File.Exists(secondary) ) {
                this.replaySecondaryPath = secondary;
            } else if( !secondary.Contains(mod.replayLogger.replayFolder) ) {
                // In case we're playing a replay from the download folder but we have the primary in our replay folder
                secondary = mod.replayLogger.replayFolder + Path.DirectorySeparatorChar + Path.GetFileName(secondary);
                if( File.Exists(secondary) ) {
                    this.replaySecondaryPath = secondary;
                }
            }

            // Always make sure the white is the primary as that person starts off the game
            if( this.replaySecondaryPath.Contains("-white.spr") ) {
                path = this.replayPrimaryPath;
                this.replayPrimaryPath = this.replaySecondaryPath;
                this.replaySecondaryPath = path;
            }

            GUISkin skin = (GUISkin)Resources.Load("_GUISkins/LobbyMenu");
            this.buttonStyle = skin.button;
            this.buttonStyle.normal.background = this.buttonStyle.hover.background;
            this.buttonStyle.normal.textColor = new Color(1f, 1f, 1f, 1f);
            this.buttonStyle.fontSize = (int)((10 + Screen.height / 72) * 0.65f);

            this.buttonStyle.hover.textColor = new Color(0.80f, 0.80f, 0.80f, 1f);

            this.buttonStyle.active.background = this.buttonStyle.hover.background;
            this.buttonStyle.active.textColor = new Color(0.60f, 0.60f, 0.60f, 1f);

            this.speedButtonStyle = new GUIStyle(this.buttonStyle);
            this.speedButtonStyle.fontSize = (int)Math.Round(this.buttonStyle.fontSize * 0.80f);

            this.realTimeButtonStyle = new GUIStyle(this.buttonStyle);
            this.realTimeButtonStyle.fontSize = (int)Math.Round(this.buttonStyle.fontSize * 1.20f);

            sceneLoaded = false;
            playerThread = new Thread(new ThreadStart(Start));
            playerThread.Start();
        }

        public Boolean OnHandleNextMessage() {
            if( msgPending ) {
                msgPending = false;
                return true;
            } else {
                return false;
            }
        }

        public void OnBattleGUI(InvocationInfo info) {
            // Bugs out visually otherwise
            deselectMethod.Invoke(info.target, null);

            // For managing the replay
            int depth = GUI.depth;

            // Container
            Color color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 1f);
            Rect container = new Rect(190f, (float)Screen.height * 0.84f, (float)(Screen.width * 0.08f), (float)Screen.height * 0.16f);
            GUI.DrawTexture(container, ResourceManager.LoadTexture("Shared/blackFiller"));
            GUI.color = color;

            GUI.depth = depth - 4;

            // Draw the header
            Rect pos = new Rect(container.x * 1.09f, container.y * 1.01f, container.width, container.height * 0.14f);
            //Rect pos = new Rect(container.x * 1.09f, container.y * 1.01f, container.width, container.height * 0.50f);

            int fontSize = GUI.skin.label.fontSize;
            color = GUI.skin.label.normal.textColor;
            GUI.skin.label.fontSize = (int)((10 + Screen.height / 72) * 0.65f);
            GUI.skin.label.normal.textColor = new Color(0.85f, 0.70f, 0.043f, 1f);
            GUI.Label(pos, "ScrollsPost Replay");
            GUI.skin.label.normal.textColor = color;
            GUI.skin.label.fontSize = fontSize;

            // Start/Pause
            pos = new Rect(container.x * 1.04f, pos.y + pos.height + 10f, container.width * 0.43f, container.height * 0.20f);
            //pos = new Rect(container.x * 1.06f, pos.y + pos.height - 6f, container.width * 0.90f, container.height * 0.0f);
            if( GUI.Button(pos, paused ? "Play" : "Pause", this.buttonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");

                paused = !paused;
                if( !paused ) wasPaused = true;
            }

            // Go to Round
            Rect goToPos = new Rect(pos.x + pos.width + 6f, pos.y, pos.width, pos.height);

            String label = "Go To";
            if( seekRound > 0 ) {
                label = "Going...";
            }

            if( GUI.Button(goToPos, label, this.buttonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                App.Popups.ShowTextInput(this, "", "Can be a round that has always passed or one in the future.", "round", "Round Seek", "Enter a Round to Go To:", "Seek");
            }

            // Speed changes
            Rect speedPos = new Rect(pos.x, pos.y + pos.height + 8f, (container.width * 0.90f) * 0.32f, pos.height);

            // Slower
            float newSpeed = speed <= 1.75f ? Math.Max(speed + 0.25f, 1.25f) : 2f;
            if( speed < 2f && GUI.Button(speedPos, String.Format("Slower", newSpeed * 100), this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = newSpeed;
            }

            // Normal
            speedPos = new Rect(speedPos.x + speedPos.width + (container.width * 0.015f), speedPos.y, speedPos.width, speedPos.height);
            if( GUI.Button(speedPos, "Normal", this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = 1;
            }

            // Faster
            newSpeed = speed >= 0.50f ? Math.Min(0.75f, speed - 0.25f) : 0.25f;

            speedPos = new Rect(speedPos.x + speedPos.width + (container.width * 0.015f), speedPos.y, speedPos.width, speedPos.height);
            if( newSpeed > 0.25f && GUI.Button(speedPos, "Faster", this.speedButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                speed = newSpeed;
            }

            // Speed cap
            pos = new Rect(pos.x, speedPos.y + pos.height + 8f, container.width * 0.90f, pos.height);
            if( GUI.Button(pos, realtime ? "Disable Real Time" : "Enable Real Time", this.realTimeButtonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                realtime = !realtime;
            }

            GUI.depth = depth;
        }

        public Boolean OnBattleUIShowEndTurn(InvocationInfo info) {
            return true;
        }
        
        public Boolean OnBattleDelay(InvocationInfo info) {
            return seekRound > 0 || speed > 1.5f;
        }

        public void OnBattleEffectDone(InvocationInfo info) {
            EffectMessage msg = (EffectMessage)effectField.GetValue(info.target);
            if( msg.type == "TurnBegin" ) {
                currentRound = (int)Math.Round((msg as EMTurnBegin).turn / 2f);
                if( currentRound == seekRound ) {
                    seekRound = 0;
                }

                internalPause = false;
            }
        }

        
        public void OnAnimationUpdate(InvocationInfo info) {
            if( seekRound > 0 || speed > 1.5f ) {
                float frame = (info.target as AnimPlayer).getFrameAnimation().getNumFrames() * 2f;
                if( ((float)animFrameField.GetValue(info.target)) < frame ) {
                    animFrameField.SetValue(info.target, frame);
                }
            }
        }

        public void OnTweenLaunch(InvocationInfo info) {
            Hashtable args = (Hashtable)info.arguments[1];
            if( args.ContainsKey("time") ) {
                args["time"] = 0.0f;
            }
        }

        private void Delay(int ms) {
            if( seekRound > 0 ) {
                ms = Math.Min(ms, 50);
            } else {
                ms = (int)Math.Round(ms * speed);
            }

            Thread.Sleep(ms);
        }

        // Round seeking
        public void PopupCancel(String type) {

        }

        public void PopupOk(String type) {

        }

        public void PopupOk(String type, String choice) {
            if( type == "round" ) {
                seekRound = Convert.ToInt16(choice);
                if( seekRound <= currentRound ) {
                    rewind = true;
                    internalPause = false;
                }
            }
        }

        // Stop a replay
        public void Stop() {
            finished = true;
            playerThread.Abort();
            App.Communicator.setData("");
            SceneLoader.loadScene("_Lobby");
        }

        // Initial replay start
        private void Start() {
            deselectMethod = typeof(BattleMode).GetMethod("deselectAllTiles", BindingFlags.Instance | BindingFlags.NonPublic);
            effectField = typeof(BattleMode).GetField("currentEffect", BindingFlags.NonPublic | BindingFlags.Instance);
            animFrameField = typeof(AnimPlayer).GetField("_fframe", BindingFlags.NonPublic | BindingFlags.Instance);

            using( StreamReader primary = new StreamReader(this.replayPrimaryPath) ) {
                if( String.IsNullOrEmpty(this.replaySecondaryPath) ) {
                    ParseStreams(primary);
                } else {
                    using( StreamReader secondary = new StreamReader(this.replaySecondaryPath) ) {
                        ParseStreams(primary, secondary);
                    }
                }
            }
        }

        // Handle pulling lines out of the replays for parsing
        private void ParseStreams(StreamReader primary, StreamReader secondary=null) {
            // Grab the config
            String line = primary.ReadLine();
            String[] parts = line.Split(new char[] { '|' }, 2);

            metadata = new JsonReader().Read<Dictionary<String, object>>(parts[1]);
            // Sort out the IDs
            String primaryID;
            String secondaryID;
            if( primaryType.Equals("white") ) {
                primaryID = (String)metadata["white-id"];
                secondaryID = (String)metadata["black-id"];
            } else {
                primaryID = (String)metadata["black-id"];
                secondaryID = (String)metadata["white-id"];
            }

            while( primary.Peek() > 0 ) {
                if( rewind || finished ) {
                    break;
                }

                line = primary.ReadLine();

                // Secondaries turn
                if( secondary != null && line.Contains("TurnBegin") && !line.Contains(primaryType) ) {
                    ParseLine(primaryID, line);
                    Delay(400);

                    String newTurn = line.Split(new char[] { '|' }, 3)[2];

                    Boolean seeking = true;

                    while( secondary.Peek() > 0 ) {
                        if( finished ) {
                            break;
                        }

                        line = secondary.ReadLine();
                        // Find the part where the new turn is
                        if( seeking && line.Contains(newTurn) ) {
                            seeking = false;

                            // Grab the initial state change
                            ParseLine(secondaryID, secondary.ReadLine(), false);
                            Delay(1000);

                        // Turn is over, swap back to primary
                        } else if( !seeking && line.Contains("TurnBegin") ) {

                            // Now reposition us on the primary pointer
                            newTurn = line.Split(new char[] { '|' }, 3)[2];

                            while( primary.Peek() > 0 ) {
                                line = primary.ReadLine();
                                if( line.Contains(newTurn) ) {
                                    ParseLine(primaryID, line);
                                    Delay(400);

                                    // Grab the initial state change
                                    ParseLine(primaryID, primary.ReadLine(), false);
                                    Delay(1000);

                                    break;
                                }
                            }

                            break;
                        // Line during the new turn
                        } else if( !seeking ) {
                            ParseLine(secondaryID, line);
                        }
                    }

                    continue;
                }

                ParseLine(primaryID, line);
            }

            if( rewind ) {
                rewind = false;
                sceneLoaded = false;
                currentRound = 0;

                Start();
            }
        }

        // Parse a single line from the replay
        private void ParseLine(String perspectiveID, String line, Boolean delay=true) {
            // Wait until the message was read off to dump it into the queue
            while( msgPending || paused ) {
                Thread.Sleep(paused ? 100 : 10);
            }

            String[] parts = line.Split(new char[] { '|' }, 3);

            // Preserve the time taken between messages
            if( !wasPaused && delay && parts[0].Equals("elapsed") ) {
                if( realtime ) {
                    Delay((int)Math.Round(1000 * Convert.ToDouble(parts[1])));
                } else {
                    Delay(1100);
                }
            }

            if( !sceneLoaded ) {
                sceneLoaded = true;
                SceneLoader.loadScene("_BattleModeView");
            }

            App.Communicator.setData(parts[2].Replace(perspectiveID, App.MyProfile.ProfileInfo.id));

            wasPaused = false;
            msgPending = true;

            if( line.Contains("TurnBegin") ) {
                internalPause = true;

                int timeout = 300;
                while( internalPause && !finished && timeout > 0 ) {
                    Thread.Sleep(10);
                    timeout -= 1;
                }
            }
        }
    }
}

