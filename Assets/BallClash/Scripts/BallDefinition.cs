using UnityEngine;

namespace BallClash
{
    public enum Role { Fighter, Magic, Marksman }
    public enum Ability { Blast, Freeze, Dash, Web, Shadow, Knock, Shield, BlackHole }

    [System.Serializable]
    public sealed class BallDefinition
    {
        public string Id, Icon, DisplayName, Skill, Description;
        public Role Role;
        public Ability Ability;
        public Color Color;
        public float Damage, Speed, Defense;

        public BallDefinition(string id, Role role, string icon, string name, string skill, Color color,
            string description, float damage, float speed, float defense, Ability ability)
        {
            Id = id; Role = role; Icon = icon; DisplayName = name; Skill = skill; Color = color;
            Description = description; Damage = damage; Speed = speed; Defense = defense; Ability = ability;
        }
    }
}
