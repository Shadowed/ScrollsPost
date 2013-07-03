using System;

namespace ScrollsPost {
    public class AccountVerifier : IOkCallback {
        //private ConfigManager config;

        public AccountVerifier(ConfigManager config) {
            //this.config = config;

            Start();
        }

        public void ShowExplanation() {
            App.Popups.ShowOk(this, "done", "Verifying Account - ScrollsPost", "We're going to be automatically joining you into ScrollsPost-Verif, sending a verification code and then leaving.\nThis stops anyone from being able to impersonate you on ScrollsPost, it only has to be done once.\n\nIf you have any questions, contact us at support@scrollspost.com", "Ok");
        }

        public void PopupOk(String type) {
        }

        public void Start() {

        }
    }
}

