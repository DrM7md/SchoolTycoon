using System;
using SchoolDirectorTycoon.Bridge;
using UnityEngine;

namespace SchoolDirectorTycoon.World
{
    [Serializable]
    public struct FacilityStyle
    {
        public Material body;
        public Material roof;
        [Tooltip("Footprint (x, z) as a fraction of a tile, and height (y) in metres.")]
        public Vector3 size;
    }

    /// <summary>Presentation only: materials and placeholder shapes. Balance numbers stay in Core.</summary>
    [CreateAssetMenu(menuName = "SDT/Campus Palette")]
    public sealed class CampusPalette : ScriptableObject
    {
        public Material ground;
        public Material tileLight;
        public Material tileDark;
        public Material path;
        public Material ghostValid;
        public Material ghostInvalid;
        [Tooltip("One entry per FacilityKind, in enum order.")]
        public FacilityStyle[] facilities = new FacilityStyle[CampusModel.FacilityCount];

        public FacilityStyle StyleFor(FacilityKind kind) => facilities[(int)kind];

        /// <summary>Empty when valid; otherwise the first problem found.</summary>
        public string Validate()
        {
            if (ground == null || tileLight == null || tileDark == null || path == null
                || ghostValid == null || ghostInvalid == null)
                return "Missing a ground, tile, path or ghost material.";
            if (facilities == null || facilities.Length != CampusModel.FacilityCount)
                return $"Expected {CampusModel.FacilityCount} facility styles.";
            for (int i = 0; i < facilities.Length; i++)
                if (facilities[i].body == null || facilities[i].roof == null || facilities[i].size.y <= 0f)
                    return $"Facility style {(FacilityKind)i} is incomplete.";
            return string.Empty;
        }
    }
}
