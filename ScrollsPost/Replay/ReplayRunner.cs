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
        private ScrollsPost.Mod mod;
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
        private int seekTurn = 0;
        private float speed = 1;

        private GUIStyle buttonStyle;
        private GUIStyle speedButtonStyle;
        private GUIStyle realTimeButtonStyle;

        private MethodInfo deselectMethod;
        private FieldInfo effectField;
        //private FieldInfo speedField;
        private FieldInfo animFrameField;

        public ReplayRunner(ScrollsPost.Mod mod, String path) {
            this.mod = mod;
            // Convert a .sgr replay to .spr
            if( path.EndsWith(".sgr") ) {
                path = ConvertScrollsGuide(path);
            }

            this.replayPrimaryPath = path;
            this.primaryType = path.EndsWith("-white.spr") ? "white" : "black";

            // Check if we have both perspectives available to us
            String secondary = path.EndsWith("-white.spr") ? path.Replace("-white.spr", "-black.spr") : path.Replace("-black.spr", "-white.spr");
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
            if( !String.IsNullOrEmpty(this.replaySecondaryPath) && this.primaryType.Equals("black") ) {
                path = this.replayPrimaryPath;
                this.replayPrimaryPath = this.replaySecondaryPath;
                this.replaySecondaryPath = path;
                this.primaryType = "white";
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
            if( seekTurn > 0 ) {
                label = "Going";
            }

            if( GUI.Button(goToPos, label, this.buttonStyle) ) {
                App.AudioScript.PlaySFX("Sounds/hyperduck/UI/ui_button_click");
                App.Popups.ShowTextInput(this, "", "Turn 1 = First Player Round 1 / Turn 2 = Second Player Round 2 / Turn 3 = First Player Round 2 and so on.", "turn", "Turn Seek", "Enter a Turn:", "Seek");
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
            newSpeed = speed > 0.25f ? Math.Min(0.75f, speed - 0.25f) : 0.25f;

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
            return speed < 0.50f;
        }

        public void OnBattleEffectDone(InvocationInfo info) {
            EffectMessage msg = (EffectMessage)effectField.GetValue(info.target);
            if( msg != null && msg.type.Equals("TurnBegin") ) {
                internalPause = false;
            }
        }

        
        public void OnAnimationUpdate(InvocationInfo info) {
            if( speed < 0.50f ) {
                float frame = (info.target as AnimPlayer).getFrameAnimation().getNumFrames() * 2f;
                if( ((float)animFrameField.GetValue(info.target)) < frame ) {
                    animFrameField.SetValue(info.target, frame);
                }
            }
        }

        public void OnTweenLaunch(InvocationInfo info) {
            if( speed < 0.50f ) {
                Hashtable args = (Hashtable)info.arguments[1];
                if( args.ContainsKey("time") ) {
                    args["time"] = 0.0f;
                }
            }
        }

        private void Delay(int ms) {
            ms = (int)Math.Round(ms * speed);

            // Run as normal
            if( ms < 200 ) {
                Thread.Sleep(ms);
            // Allow interrupts
            } else {
                float savedSpeed = speed;
                while( ms > 0 ) {
                    if( ms > 50 ) {
                        Thread.Sleep(50);
                        ms -= 50;
                    } else {
                        Thread.Sleep(0);
                        ms = 0;
                    }

                    if( rewind || speed != savedSpeed ) {
                        break;
                    }
                }
            }
        }

        // Round seeking
        public void PopupCancel(String type) {

        }

        public void PopupOk(String type) {

        }

        public void PopupOk(String type, String choice) {
            if( type == "turn" ) {
                seekTurn = Convert.ToInt16(choice);
                rewind = true;
                internalPause = false;
            }
        }

        // Handles the instant seek functionality
        private void CoalesceEvents(StreamReader sr) {
            var units = new Dictionary<object, Dictionary<object, object>>();
            var stats = new Dictionary<object, Dictionary<object, object>>();
            var idols = new Dictionary<object, Dictionary<object, object>>(); 
            var lastEvents = new Dictionary<object, object>();
            var lastHands = new Dictionary<object, object>();

            foreach( var side in new string[] { "white", "black" } ) {
                units[side] = new Dictionary<object, object>();
                stats[side] = new Dictionary<object, object>();
                idols[side] = new Dictionary<object, object>();
            }

            int lastTurn = 0;
            String lastTurnSide = "white";
            String gameInfo = "";
            String turnLine = "";

            while( sr.Peek() > 0 && seekTurn > 0 ) {
                String line = sr.ReadLine();
                if( gameInfo.Equals("") && line.Contains("GameInfo") ) {
                    gameInfo = line.Split(new char[] { '|' }, 3)[2];
                    continue;
                }

                if( !line.Contains("NewEffects") )
                    continue;

                line = line.Split(new char[] { '|' }, 3)[2];
                var msg = (Dictionary<String, object>)new JsonReader().Read<Dictionary<String, object>>(line);
                if( !msg.ContainsKey("effects") )
                    continue;

                var effects = (Dictionary<String, object>[])msg["effects"];
                foreach( var effect in effects ) {
                    if( effect.ContainsKey("MoveUnit") ) {
                        var unit = (Dictionary<String, object>)effect["MoveUnit"];
                        var fromTile = (Dictionary<String, object>)unit["from"];
                        var toTile = (Dictionary<String, object>)unit["to"];

                        units[toTile["color"]][toTile["position"]] = units[fromTile["color"]][fromTile["position"]];
                        stats[toTile["color"]][toTile["position"]] = stats[fromTile["color"]][fromTile["position"]];

                        units[fromTile["color"]].Remove(fromTile["position"]);
                        stats[fromTile["color"]].Remove(fromTile["position"]);

                    } else if( effect.ContainsKey("SummonUnit") ) {
                        var unit = (Dictionary<String, object>)(effect["SummonUnit"] as Dictionary<String, object>)["target"];
                        units[unit["color"]][unit["position"]] = effect;
                    
                    } else if( effect.ContainsKey("RemoveUnit") ) {
                        var unit = (Dictionary<String, object>)(effect["RemoveUnit"] as Dictionary<String, object>)["tile"];
                        units[unit["color"]].Remove(unit["position"]);
                        stats[unit["color"]].Remove(unit["position"]);

                    } else if( effect.ContainsKey("StatsUpdate") ) {
                        var target = (Dictionary<String, object>)(effect["StatsUpdate"] as Dictionary<String, object>)["target"];
                        stats[target["color"]][target["position"]] = effect;
                    
                    } else if( effect.ContainsKey("IdolUpdate") ) {
                        var idol = (Dictionary<String, object>)(effect["IdolUpdate"] as Dictionary<String, object>)["idol"];
                        idols[idol["color"]][idol["position"]] = effect;

                    // Various states we need
                    } else if( effect.ContainsKey("ResourcesUpdate") ) {
                        lastEvents["ResourcesUpdate"] = effect;
                    } else if( effect.ContainsKey("HandUpdate") ) {
                        lastHands[(effect["HandUpdate"] as Dictionary<String, object>)["profileId"]] = effect;
                    // Check if we're done seeking
                    } else if( effect.ContainsKey("TurnBegin") ) {
                        var turn = (Dictionary<String, object>)effect["TurnBegin"];

                        // Yup!
                        lastTurn = (int)turn["turn"];
                        lastTurnSide = (String)turn["color"];
                        turnLine = line;

                        if( lastTurn == seekTurn ) {
                            seekTurn = 0;
                        }
                    }
                }
            }

            // Seeked outside of the total rounds
            if( seekTurn > 0 ) {
                App.Popups.ShowOk(this, "", "Too Far", String.Format("You tried to seek to round {0}, but the game ends at round {1}.", seekTurn, lastTurn), "Ok");
                return;
            }

            // Now play us off keyboard cat
            var resEffects = new List<Dictionary<String, object>>();
            var resStates = new Dictionary<String, object>();

            foreach( var side in new string[] { "white", "black" } ) {
                var state = new Dictionary<String, object>();
                var board = new Dictionary<String, object>();
                var idolHP = new Dictionary<int, int>();
                var tiles = new List<Dictionary<String, object>>();

                // Figure out units on board
                foreach( var pair in units[side] ) {
                    var value = (Dictionary<String, object>)pair.Value;

                    var summon = (Dictionary<String, object>)value["SummonUnit"];
                    var card = (Dictionary<String, object>)summon["card"];

                    var tile = new Dictionary<String, object>();
                    tile["card"] = card;
                    tile["position"] = pair.Key;

                    var stat = (Dictionary<String, object>)(stats[side][pair.Key] as Dictionary<String, object>)["StatsUpdate"];
                    tile["ap"] = stat["ap"];
                    tile["ac"] = stat["ac"];
                    tile["hp"] = stat["hp"];
                    if( stat.ContainsKey("buffs") ) {
                        tile["buffs"] = stat["buffs"];
                    }

                    tiles.Add(tile);
                }

                // Figure out idol health
                idolHP[0] = 10;
                idolHP[1] = 10;
                idolHP[2] = 10;
                idolHP[3] = 10;
                idolHP[4] = 10;

                foreach( var pair in idols[side] ) {
                    var idol = (Dictionary<String, object>)(pair.Value as Dictionary<String, object>)["IdolUpdate"];
                    idolHP[(int)pair.Key] = (int)(idol["idol"] as Dictionary<String, object>)["hp"];
                }

                // Finish off the rest of the states
                board["idols"] = new List<int>(idolHP.Values).ToArray();
                board["tiles"] = tiles.ToArray();
                board["color"] = side;

                state["board"] = board;
                state["playerName"] = metadata[side + "-name"];

                var resources = (Dictionary<String, object>)lastEvents["ResourcesUpdate"];

                resources = (Dictionary<String, object>)resources["ResourcesUpdate"];
                state["assets"] = resources[side + "Assets"];

                resStates[side] = state;
            }

            // Write out state
            var res = new Dictionary<String, object>();
            res["msg"] = "GameState";
            res["turn"] = lastTurn;
            res["activeColor"] = lastTurnSide;
            res["whiteGameState"] = resStates["white"];
            res["blackGameState"] = resStates["black"];
            res["phase"] = "Init";

            var stateText = new JsonWriter().Write(res);

            // Write out effects
            // Hands
            foreach( var pair in lastHands ) {
                resEffects.Add((Dictionary<String, object>)pair.Value);
            }

            res = new Dictionary<String, object>();
            res["msg"] = "NewEffects";
            res["effects"] = resEffects.ToArray();

            var effectText = new JsonWriter().Write(res);

            // Send it off
            sceneLoaded = true;
            SceneLoader.loadScene("_BattleModeView");

            internalPause = true;
            foreach( var line in new string[] { gameInfo, stateText, effectText, turnLine } ) {
                App.Communicator.setData(line);
                msgPending = true;

                while( msgPending ) {
                    Thread.Sleep(10);
                }

                Thread.Sleep(750);
            }

            internalPause = false;
            while( internalPause ) {
                Thread.Sleep(10);
            }
        }

        // Stop a replay
        public void Stop() {
            finished = true;
            playerThread.Abort();
            App.Communicator.setData("");
            SceneLoader.loadScene("_Lobby");
        }

        // Convert a ScrollsGuide replay into ScrollsPost
        private String ConvertScrollsGuide(String path) {
            var metadata = new Dictionary<String, object>();
            metadata["played-at"] = mod.TimeSinceEpoch();

            var converted = new List<String>();

            using( StreamReader sr = new StreamReader(path) ) {
                while( sr.Peek() > 0 ) {
                    String line = sr.ReadLine();
                    if( String.IsNullOrEmpty(line) ) continue;

                    // We need to figure out the metadata for the replay primarily
                    if( line.Contains("ServerInfo") ) {
                        var msg = new JsonReader().Read<Dictionary<String, object>>(line);
                        metadata["version"] = msg["version"];
                        metadata["format-version"] = msg["version"];
                        continue;

                    } else if( line.Contains("GameInfo") ) {
                        var msg = new JsonReader().Read<Dictionary<String, object>>(line);

                        metadata["white-name"] = msg["white"];
                        metadata["white-id"] = (msg["whiteAvatar"] as Dictionary<String, object>)["profileId"];
                        metadata["black-name"] = msg["black"];
                        metadata["black-id"] = (msg["blackAvatar"] as Dictionary<String, object>)["profileId"];
                        metadata["deck"] = msg["deck"];
                        metadata["game-id"] = msg["gameId"];
                        metadata["perspective"] = msg["color"];

                    } else if( !metadata.ContainsKey("played-at") && line.Contains("\"msg\":\"Ping\"") ) {
                        var msg = new JsonReader().Read<Dictionary<String, object>>(line);
                        metadata["played-at"] = Math.Round(Convert.ToDouble(msg["time"]) / 1000);
                    } else if( line.Contains("NewEffects") && line.Contains("\"EndGame\":{") ) {
                        metadata["winner"] = line.Contains("\"winner\":\"white\"") ? "white" : "black";
                    }

                    float elapsed = 0f;
                    if( line.Contains("NewEffect") ) {
                        elapsed = 1.2f;
                    } else if( line.Contains("GamechatMessage") ) {
                        elapsed = 0.1f;
                    } else if( line.Contains("CardInfo") ) {
                        elapsed = 0.3f;
                    } else if( !line.Contains("Ping") ) {
                        elapsed = 0.2f;
                    }
         
                    converted.Add(String.Format("elapsed|{0}|{1}", elapsed, line));
                }
            }

            converted.Insert(0, String.Format("metadata|{0}", new JsonWriter().Write(metadata)));

            String filename = String.Format("{0}-{1}.spr", metadata["game-id"], metadata["perspective"].Equals("white") ? 0 : 1);
            String convertedPath = path.Replace(Path.GetFileName(path), filename);

            File.WriteAllLines(convertedPath, converted.ToArray());
            try {
                File.Delete(path);
            } catch( Exception e ) {
                Console.WriteLine("***** EXCEPTION {0}", e.ToString());
            }

            return convertedPath;
        }

        // Check the format and see if we need to do any upgrading
        private String[] UpgradeFile(Dictionary<String, object> metadata, String path, StreamReader sr) {
            String[] parts = (metadata["version"] as String).Split(new char[] { '.' }, 3);
            float version = Convert.ToSingle(parts[0] + "." + parts[1]);

            float format_version = version;
            if( metadata.ContainsKey("format-version") ) {
                parts = (metadata["format-version"] as String).Split(new char[] { '.' }, 3);
                format_version = Convert.ToSingle(parts[0] + "." + parts[1]);
            }

            if( version >= 0.96f || format_version >= 0.96f )
                return new string[] {};

            // Upgrade from 0.95.x -> 0.96.0
            App.Popups.ShowInfo("Replay Upgrade", String.Format("We're upgrading {0} to Scrolls 0.96.0 format.\n\nThis will only take a minute and won't have to be done again for this file.", Path.GetFileName(path)));

            metadata["format-version"] = "0.96.0";
            rewind = true;

            // Start upgrading
            List<String> lines = new List<String>();
            lines.Add(String.Format("metadata|{0}", new JsonWriter().Write(metadata)));

            while( sr.Peek() > 0 ) {
                String line = sr.ReadLine();
                // Only need to upgrade SummonUnit thankfully
                if( !line.Contains("SummonUnit") ) {
                    lines.Add(line);
                    continue;
                }

                parts = line.Split(new char[] { '|' }, 3);
                var msg = (Dictionary<String, object>)new JsonReader().Read<Dictionary<String, object>>(parts[2]);

                var cardInfo = new Dictionary<String, object>();
                var effects = (Dictionary<String, object>[])msg["effects"];
                foreach( var effect in effects ) {
                    if( effect.ContainsKey("CardPlayed") ) {
                        var played = (Dictionary<String, object>)effect["CardPlayed"];
                        var card = (Dictionary<String, object>)played["card"];
                        if( !card.ContainsKey("isToken") )
                            card["isToken"] = false;

                        cardInfo[String.Format("{0},{1}", played["color"], card["typeId"])] = card;

                    } else if( effect.ContainsKey("SummonUnit") ) {
                        var summon = (effect["SummonUnit"] as Dictionary<String, object>);
                        var target = (Dictionary<String, object>)summon["target"];
                        var typeID = (int)(summon["unit"] as Dictionary<String, object>)["cardTypeId"];

                        String key = String.Format("{0},{1}", target["color"], typeID);
                        if( cardInfo.ContainsKey(key) ) {
                            summon["card"] = cardInfo[key];
                        } else {
                            var dummy = new Dictionary<String, object>();
                            dummy["id"] = -1;
                            dummy["typeId"] = typeID;
                            dummy["isToken"] = (summon["unit"] as Dictionary<String, object>)["isToken"];
                            dummy["tradable"] = true;
                            dummy["level"] = 0;
                            summon["card"] = dummy;
                        }

                        summon.Remove("unit");
                    }
                }

                lines.Add(String.Format("{0}|{1}|{2}", parts[0], parts[1], new JsonWriter().Write(msg)));
            }

            // Yay done
            App.Popups.KillCurrentPopup();

            return lines.ToArray();
        }

        // Initial replay start
        private Dictionary<String, object> PullMetadata(StreamReader sr) {
            String line = sr.ReadLine();
            String[] parts = line.Split(new char[] { '|' }, 2);
            return new JsonReader().Read<Dictionary<String, object>>(parts[1]);
        }

        private void Start() {
            deselectMethod = typeof(BattleMode).GetMethod("deselectAllTiles", BindingFlags.Instance | BindingFlags.NonPublic);
            effectField = typeof(BattleMode).GetField("currentEffect", BindingFlags.NonPublic | BindingFlags.Instance);
            animFrameField = typeof(AnimPlayer).GetField("_fframe", BindingFlags.NonPublic | BindingFlags.Instance);

            String[] primaryUpgrade;
            String[] secondaryUpgrade = new string[] {};

            using( StreamReader primary = new StreamReader(this.replayPrimaryPath) ) {
                metadata = PullMetadata(primary);
                primaryUpgrade = UpgradeFile(metadata, this.replayPrimaryPath, primary);

                // Single perspective
                if( String.IsNullOrEmpty(this.replaySecondaryPath) ) {
                    if( primaryUpgrade.Length == 0 )
                        ParseStreams(primary);
               
                // Multi perspective replay
                } else {
                    using( StreamReader secondary = new StreamReader(this.replaySecondaryPath) ) {
                        secondaryUpgrade = UpgradeFile(PullMetadata(secondary), this.replaySecondaryPath, secondary);
                        if( primaryUpgrade.Length == 0 && secondaryUpgrade.Length == 0 ) {
                            ParseStreams(primary, secondary);
                        }
                    }
                }
            }

            Boolean upgraded = false;
            if( primaryUpgrade.Length > 0 ) {
                upgraded = true;
                File.WriteAllLines(this.replayPrimaryPath, primaryUpgrade);
            }

            if( secondaryUpgrade.Length > 0 ) {
                upgraded = true;
                File.WriteAllLines(this.replaySecondaryPath, secondaryUpgrade);
            }

            if( upgraded )
                Start();
        }

        // Handle pulling lines out of the replays for parsing
        private void ParseStreams(StreamReader primary, StreamReader secondary=null) {
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

            if( seekTurn > 0 ) {
                CoalesceEvents(primary);
            }

            while( primary.Peek() > 0 ) {
                if( rewind || finished ) {
                    break;
                }

                String line = primary.ReadLine();

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

