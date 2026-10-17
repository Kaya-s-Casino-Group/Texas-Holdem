using UnityEngine;

namespace CardPresentation.Tests
{
    [AddComponentMenu("")]
    public sealed class SpecializedHandZoneViewForTests : HandZoneView
    {
        public float ExportTestSetting;
        public CardZoneView Self;
        public GameObject Owner;
        public Transform Origin;
        public Object AdditionalReference;
    }
}
