using System.Collections.Generic;

namespace AiEditor
{
    /// <summary>
    /// Centralized repository for all AI node descriptions
    /// </summary>
    public static class NodeDescriptions
    {
        private static Dictionary<string, string> descriptions = new Dictionary<string, string>()
        {
            // Condition Nodes
            { "IfSelf", "Targets yourself for subsequent condition checks (HP, Armor). Use before checking your own stats." },
            { "IfSelfHP", "Checks your current HP percentage. Use with > or < and a number (e.g., 'If Self HP>50' means if your HP is above 50%)." },
            { "IfComs", "Enables team vision. Allows you to detect enemies seen by your allies, even if you can't see them directly." },
            { "IfEnemy", "Checks if an enemy tank is visible in your vision cone. Sets the enemy as your current target for subsequent actions." },
            { "IfAlly", "Checks if an ally tank is visible in your vision cone. Sets the ally as your current target for subsequent actions." },
            { "IfAny", "Checks if ANY tank (enemy or ally) is visible. Selects the closest one as the target." },
            { "IfRifle", "Checks if the current target has a rifle-type weapon equipped." },
            { "IfHP", "Checks the target's HP. You can check actual value or percentage . Enter a number (e.g., 'If HP>50' means if target's HP is above 50)." },
            { "IfLight", "Checks the target's armor type." },
            { "IfHeavy", "Checks the target's armor type." },
            { "IfRange", "Checks the distance to the current target. Enter a number (e.g., 'If Range<20' means if target is within 20 units)." },
            { "IfTag", "Checks if the current target has a specific tag number or range of numbers. Use with =, <, or > and a number." },
            
            // Action Nodes - Navigation
            { "Wander", "Move randomly within the wander range. Good for exploration or idle behavior." },
            { "Move", "Move forward in the current facing direction." },
            { "Wait", "Stop all movement. Remain stationary." },
            { "Chase", "Move towards and follow the current target (set by IfEnemy, IfAlly, etc.)." },
            { "Flee", "Move away from the current target. Used to escape from danger or avoid collision." },
          
            
            // Action Nodes - Turret
            { "Fire", "...it shoots. However if used with LeadTarget under it then it will make sure it is aimed before shooting" },
            { "LeadTarget", "Adjust aim to lead a moving target by a specified distance. Use with a number (e.g., 'LeadTarget 5'). Will default to 0 which centers target" },
            { "Tag", "slaps number stickers on visible targets so your team can prioritize targets instead of only using the closest target (e.g. 2 enemies attacking but the further one is nearly dead)" },
            { "RotateUp", "Rotate the turret upward by a specified angle. Use with a number (degrees)." },
            { "RotateDown", "Rotate the turret downward by a specified angle. Use with a number (degrees)." },
            { "RotateLeft", "Rotate the turret left by a specified angle. Use with a number (degrees)." },
            { "RotateRight", "Rotate the turret right by a specified angle. Use with a number (degrees)." },
            { "AlignLeft", "Rotate the turret to point left relative to the chassis" },
            { "AlignRight", "Rotate the turret to point right relative to the chassis" },
            { "AlignFront", "Rotate the turret to point forward relative to the chassis" },
            { "AlignBack", "Rotate the turret to point backward relative to the chassis" },
            
            // Special
            { "SubAI", "References a separate AI tree to execute. Useful for modular, reusable AI behaviors." },
            { "Cycle", "Cycles through connected nodes in sequence, executing one per iteration. Returns to the first after completing all." }
        };
        
        /// <summary>
        /// Gets the description for a node based on its label or method name
        /// </summary>
        /// <param name="nodeLabel">The node's display label (e.g., "If Enemy", "Chase", "If HP > 50")</param>
        /// <returns>The description string, or a default message if not found</returns>
        public static string GetDescription(string nodeLabel)
        {
            if (string.IsNullOrEmpty(nodeLabel))
                return "No description available.";
            
            // Convert the label to a method name to match our dictionary keys
            float numericValue;
            string methodName = AiMethodConverter.ConvertToMethodName(nodeLabel, out numericValue);
            
            // Special handling for SubAI nodes
            if (methodName.StartsWith("SubAI_"))
            {
                return descriptions.ContainsKey("SubAI") ? descriptions["SubAI"] : "References a separate AI tree.";
            }
            
            // Check if we have a description for this method name
            if (descriptions.ContainsKey(methodName))
            {
                return descriptions[methodName];
            }
            
            // Fallback: try to match partial node names for custom variations
            string cleanLabel = nodeLabel.Trim().ToLower();
            if (cleanLabel.Contains("cycle"))
                return descriptions.ContainsKey("Cycle") ? descriptions["Cycle"] : "Cycles through connected nodes.";
            
            return $"Node: {nodeLabel}\nNo description available.";
        }
        
        /// <summary>
        /// Checks if a description exists for the given node label
        /// </summary>
        public static bool HasDescription(string nodeLabel)
        {
            if (string.IsNullOrEmpty(nodeLabel))
                return false;
                
            float numericValue;
            string methodName = AiMethodConverter.ConvertToMethodName(nodeLabel, out numericValue);
            
            if (methodName.StartsWith("SubAI_"))
                return descriptions.ContainsKey("SubAI");
                
            return descriptions.ContainsKey(methodName);
        }
    }
}
