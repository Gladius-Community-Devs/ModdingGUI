using System;
using System.IO;
using System.Text.Json;

namespace ModdingGUI
{
    public partial class frmMain
    {
        // Model class to store randomizer settings
        private class RandomizerSettings
        {
            // Checkbox settings
            public bool RandomHeroes { get; set; }
            public bool RandomTeam { get; set; }
            public bool NoRecruits { get; set; }
            public bool PermaDeath { get; set; }
            public bool IngameRandom { get; set; }
            public bool RandomStatsets { get; set; }
            public bool RandomItemsets { get; set; }
            public bool Random40Glads { get; set; }
            public bool MaxMoney { get; set; }

            // Radio button selections
            public string HeroSelection { get; set; } // "Valens" or "Ursula"
            public string GameVersion { get; set; }   // "Vanilla", "Ragnaroks", or "Leonarths"
            
            // Text input settings
            public string Seed { get; set; }

            // Default constructor for deserialization
            public RandomizerSettings() { }

            // Constructor to create settings from current UI state
            public RandomizerSettings(frmMain form)
            {
                // Checkbox settings
                this.RandomHeroes = form.chbRandomHeroes.Checked;
                this.RandomTeam = form.chbRandomTeam.Checked;
                this.NoRecruits = form.chbRandomNoRecruits.Checked;
                this.PermaDeath = form.chbRandomPermaDeath.Checked;
                this.IngameRandom = form.chbIngameRandom.Checked;
                this.RandomStatsets = form.chbRandomStatsets.Checked;
                this.RandomItemsets = form.chbRandomItemsets.Checked;
                this.Random40Glads = form.chbRandom40Glads.Checked;
                this.MaxMoney = form.chbRandomMaxMoney.Checked;

                // Get hero selection
                this.HeroSelection = form.rbnValens.Checked ? "Valens" : "Ursula";
                
                // Get game version
                if (form.rbnVanilla.Checked)
                    this.GameVersion = "Vanilla";
                else if (form.rbnRagnaroks.Checked)
                    this.GameVersion = "Ragnaroks";
                else if (form.rbnLeonarths.Checked)
                    this.GameVersion = "Leonarths";
                
                // Text input
                this.Seed = form.txtSeed.Text;
            }

            // Apply stored settings to the UI
            public void ApplyToForm(frmMain form)
            {
                // Apply checkbox settings
                form.chbRandomHeroes.Checked = this.RandomHeroes;
                form.chbRandomTeam.Checked = this.RandomTeam;
                form.chbRandomNoRecruits.Checked = this.NoRecruits;
                form.chbRandomPermaDeath.Checked = this.PermaDeath;
                form.chbIngameRandom.Checked = this.IngameRandom;
                form.chbRandomStatsets.Checked = this.RandomStatsets;
                form.chbRandomItemsets.Checked = this.RandomItemsets;
                form.chbRandom40Glads.Checked = this.Random40Glads;
                form.chbRandomMaxMoney.Checked = this.MaxMoney;

                // Apply hero selection
                if (this.HeroSelection == "Valens")
                    form.rbnValens.Checked = true;
                else if (this.HeroSelection == "Ursula")
                    form.rbnUrsula.Checked = true;
                
                // Apply game version
                if (this.GameVersion == "Vanilla")
                    form.rbnVanilla.Checked = true;
                else if (this.GameVersion == "Ragnaroks")
                    form.rbnRagnaroks.Checked = true;
                else if (this.GameVersion == "Leonarths")
                    form.rbnLeonarths.Checked = true;
                
                // Apply text input
                form.txtSeed.Text = this.Seed;
            }
        }

        // Constants for the settings file
        private const string RANDOMIZER_SETTINGS_FILENAME = "randomizer_settings.json";
        
        // Save the current randomizer settings to a JSON file in the project folder
        private void SaveRandomizerSettings(string projectFolder)
        {
            try
            {
                // Create settings object from current UI state
                var settings = new RandomizerSettings(this);
                
                // Define path to settings file
                string settingsFilePath = Path.Combine(projectFolder, RANDOMIZER_SETTINGS_FILENAME);
                
                // Serialize to JSON
                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(settings, options);
                
                // Write to file
                File.WriteAllText(settingsFilePath, jsonString);
                
                AppendLog($"Randomizer settings saved to {RANDOMIZER_SETTINGS_FILENAME}", SuccessColor, rtbPackOutput);
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to save randomizer settings: {ex.Message}", ErrorColor, rtbPackOutput);
            }
        }
        
        // Load randomizer settings from the project folder
        private bool LoadRandomizerSettings(string projectFolder)
        {
            try
            {
                // Define path to settings file
                string settingsFilePath = Path.Combine(projectFolder, RANDOMIZER_SETTINGS_FILENAME);
                
                // Check if settings file exists
                if (!File.Exists(settingsFilePath))
                {
                    return false;
                }
                
                // Read JSON from file
                string jsonString = File.ReadAllText(settingsFilePath);
                
                // Deserialize from JSON
                var settings = JsonSerializer.Deserialize<RandomizerSettings>(jsonString);
                
                if (settings != null)
                {
                    // Apply settings to UI
                    settings.ApplyToForm(this);
                    AppendLog($"Randomizer settings loaded from {RANDOMIZER_SETTINGS_FILENAME}", InfoColor, rtbPackOutput);
                    return true;
                }
                
                return false;
            }
            catch (Exception ex)
            {
                AppendLog($"Failed to load randomizer settings: {ex.Message}", ErrorColor, rtbPackOutput);
                return false;
            }
        }
    }
}
