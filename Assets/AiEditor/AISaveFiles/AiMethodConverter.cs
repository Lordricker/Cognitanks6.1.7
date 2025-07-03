using System.Text.RegularExpressions;
using UnityEngine;

namespace AiEditor
{
    /// <summary>
    /// Converts AI node labels to executable method names and extracts numeric values
    /// </summary>
    public static class AiMethodConverter
    {
        /// <summary>
        /// Converts a node label to a method name and extracts any numeric values
        /// </summary>
        /// <param name="nodeLabel">The original node label (e.g., "If HP > 50%")</param>
        /// <param name="numericValue">Output: any numeric value found in the label</param>
        /// <returns>Method name (e.g., "IfHP")</returns>
        public static string ConvertToMethodName(string nodeLabel, out float numericValue)
        {
            numericValue = 0f;
            
            if (string.IsNullOrEmpty(nodeLabel))
                return "Unknown";

            string cleanLabel = nodeLabel.Trim().ToLower();
            
            // Extract numeric values from labels like "If HP > 50%" or "If Range < 10"
            Match numMatch = Regex.Match(cleanLabel, @"(\d+(?:\.\d+)?)");
            if (numMatch.Success)
            {
                float.TryParse(numMatch.Groups[1].Value, out numericValue);
            }
            
            // Convert common condition patterns
            if (cleanLabel.StartsWith("if"))
            {
                if (cleanLabel.Contains("self"))
                    return "IfSelf";
                if (cleanLabel.Contains("enemy"))
                    return "IfEnemy";
                if (cleanLabel.Contains("ally"))
                    return "IfAlly";
                if (cleanLabel.Contains("any"))
                    return "IfAny";
                if (cleanLabel.Contains("rifle") || cleanLabel.Contains("weapon"))
                    return "IfRifle";
                if (cleanLabel.Contains("hp") || cleanLabel.Contains("health"))
                    return "IfHP";
                if (cleanLabel.Contains("armor") || cleanLabel.Contains("armour"))
                    return "IfArmor";
                if (cleanLabel.Contains("range") || cleanLabel.Contains("distance"))
                    return "IfRange";
                if (cleanLabel.Contains("tag"))
                    return "IfTag";
            }
            
            // Convert common action patterns
            if (cleanLabel.Contains("fire") || cleanLabel.Contains("shoot"))
                return "Fire";
            if (cleanLabel.Contains("wander") || cleanLabel.Contains("roam"))
                return "Wander";
            if (cleanLabel.Contains("move") || cleanLabel.Contains("go"))
                return "Move";
            if (cleanLabel.Contains("stop") || cleanLabel.Contains("halt"))
                return "Stop";
            if (cleanLabel.Contains("chase") || cleanLabel.Contains("follow"))
                return "Chase";
            if (cleanLabel.Contains("flee") || cleanLabel.Contains("escape"))
                return "Flee";
            if (cleanLabel.Contains("patrol"))
                return "Patrol";
            if (cleanLabel.Contains("guard") || cleanLabel.Contains("defend"))
                return "Guard";
            
            // For SubAI nodes, return the label as method name (will be handled separately)
            if (cleanLabel.Contains("subai") || cleanLabel.Contains("sub-ai") || cleanLabel.Contains("sub ai"))
                return "SubAI_" + SanitizeMethodName(nodeLabel);
            
            // Default: sanitize the label and use it as method name
            return SanitizeMethodName(nodeLabel);
        }
        
        /// <summary>
        /// Determines the node type based on the label
        /// </summary>
        public static AiNodeType DetermineNodeType(string nodeLabel)
        {
            if (string.IsNullOrEmpty(nodeLabel))
                return AiNodeType.Action;
                
            string cleanLabel = nodeLabel.Trim().ToLower();
            
            if (cleanLabel.StartsWith("if") || cleanLabel.Contains("condition") || 
                cleanLabel.Contains("check") || cleanLabel.Contains("when"))
                return AiNodeType.Condition;
                
            if (cleanLabel.Contains("subai") || cleanLabel.Contains("sub-ai") || cleanLabel.Contains("sub ai"))
                return AiNodeType.SubAI;
                
            return AiNodeType.Action;
        }
        
        /// <summary>
        /// Sanitizes a string to be a valid C# method name
        /// </summary>
        private static string SanitizeMethodName(string input)
        {
            if (string.IsNullOrEmpty(input))
                return "Unknown";
                
            // Remove special characters and spaces, capitalize first letter of each word
            string result = Regex.Replace(input, @"[^a-zA-Z0-9]", " ");
            string[] words = result.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
            
            string methodName = "";
            foreach (string word in words)
            {
                if (word.Length > 0)
                {
                    methodName += char.ToUpper(word[0]) + word.Substring(1).ToLower();
                }
            }
            
            return string.IsNullOrEmpty(methodName) ? "Unknown" : methodName;
        }
    }
}
