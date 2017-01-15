﻿using System.Runtime.InteropServices;
using OpenTK;

namespace Neo.IO.Files.Sky.WoD
{
	public class LightEntryData
    {
        public LightEntryData(LightEntry e)
        {
	        this.Id = e.Id;
	        this.MapId = e.MapId;
	        this.Position = e.Position;
	        this.InnerRadius = e.InnerRadius;
	        this.OuterRadius = e.OuterRadius;
	        this.RefParams = e.RefParams;
	        this.Water = e.Water;
	        this.Sunset = e.Sunset;
	        this.Other = e.Other;
	        this.Death = e.Death;
        }

        public readonly int Id;
        public readonly int MapId;
        public Vector3 Position;
        public float InnerRadius;
        public float OuterRadius;
        public readonly int RefParams;
        public readonly int Water;
        public readonly int Sunset;
        public readonly int Other;
        public readonly int Death;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LightEntry
    {
        public readonly int Id;
        public readonly int MapId;
        public readonly Vector3 Position;
        public readonly float InnerRadius;
        public readonly float OuterRadius;
        public readonly int RefParams;
        public readonly int Water;
        public readonly int Sunset;
        public readonly int Other;
        public readonly int Death;
        private readonly int unk1, unk2, unk3;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LightDataEntry
    {
        public readonly uint id;
        public readonly uint skyId;
        public readonly uint timeValues;
        public readonly uint globalDiffuse;
        public readonly uint globalAmbient;
        public readonly uint skyColor0;
        public readonly uint skyColor1;
        public readonly uint skyColor2;
        public readonly uint skyColor3;
        public readonly uint skyColor4;
        public readonly uint fogColor;
        public readonly uint sunColor;
        public readonly uint haloColor;
        public readonly uint cloudColor;
        private readonly uint unk1;
        private readonly uint unk2;
        private readonly uint unk3;
        public readonly uint darkWater;
        public readonly uint lightWater;
        public readonly uint shadowColor;
        public readonly uint unk4;
        public readonly float fogEnd;
        public readonly float fogScaler;
	    private readonly float unk5;
        public readonly float fogDensity;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct LightParamsEntry
    {
        public readonly int Id;
        public readonly int HightlightSky;
        public readonly int LightSkyboxId;
        public readonly int CloudTypeId;
        public readonly float Glow;
        public readonly float WaterAlphaShallow;
        public readonly float WaterAlphaDeep;
        public readonly float OceanAlphaShallow;
        public readonly float OceanAlphaDeep;
        public readonly int flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ZoneLightPoint
    {
        public readonly int Id;
        public readonly int RefZoneLight;
        public readonly float X, Z;
        public readonly int Counter;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct DbcZoneLight
    {
        public readonly int Id;
        public readonly int ofsName;
        public readonly int MapId;
        public readonly int RefLight;
    }
}