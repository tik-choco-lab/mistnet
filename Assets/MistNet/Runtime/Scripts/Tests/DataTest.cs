using MistNet.DNVE3;
using NUnit.Framework;
using UnityEngine;

namespace MistNet.Runtime.Scripts.Tests
{
    public class DataTest
    {
        private DNVE3DataStore _dnve3DataStore;
        private Vector3[] nodes;
        private Vector3 centerOld;
        private Vector3 centerNew;

        [SetUp]
        public void Setup()
        {
            _dnve3DataStore = new DNVE3DataStore();

        }

        [Test]
        public void TestSelfDataInitialization()
        {
            var selfData = new SpatialHistogramData
            {
                Position = new Position(new Vector3(0, 0, 32)),
                Hists = new float[, ]
                {
                    {1, 2, 3},
                    {4, 5, 6},
                    {7, 8, 9}
                }
            };
            _dnve3DataStore.SelfData = selfData;
            selfData.Hists = new float[, ]
            {
                {9, 8, 7},
                {6, 5, 4},
                {3, 2, 1}
            };
            Debug.Log(_dnve3DataStore.SelfData.Hists[0, 0]); // Should log 9 if reference is maintained
        }
    }
}
