﻿using System;
using System.Collections.Generic;
using UnityEngine;
using BahaTurret;
using DarkMultiPlayer;
using MessageStream2;


namespace BDDMP
{
	[KSPAddon(KSPAddon.Startup.Instantly, true)]
	public class BDDMPSynchronizer : MonoBehaviour
	{
		static BDDMPSynchronizer singleton;
        //FX Sync
        const int syncFXHz = 40;
        static float lastFXSync = 0;
        static int tickCount = 0;
        const int updateHistoryMinutesToLive = 3;

        //Update Entries
        static List<BDArmoryDamageUpdate> damageEntries = new List<BDArmoryDamageUpdate> ();
        static List<BDArmoryBulletHitUpdate> bulletHitEntries = new List<BDArmoryBulletHitUpdate> ();
        static List<BDArmoryExplosionUpdate> explosionEntries = new List<BDArmoryExplosionUpdate> ();
        static List<BDArmoryTracerUpdate> tracerEntries = new List<BDArmoryTracerUpdate> ();

        //Update Completion Entries
        static List<BDArmoryDamageUpdate> damageEntriesCompleted = new List<BDArmoryDamageUpdate> ();
        static List<BDArmoryBulletHitUpdate> bulletHitEntriesCompleted = new List<BDArmoryBulletHitUpdate> ();
        static List<BDArmoryExplosionUpdate> explosionEntriesCompleted = new List<BDArmoryExplosionUpdate> ();
        static List<BDArmoryTracerUpdate> tracerEntriesCompleted = new List<BDArmoryTracerUpdate> ();

        //Tracking Dictionaries
        static Dictionary<Guid, LineRenderer> tracerTracking = new Dictionary<Guid, LineRenderer> ();
        static Dictionary<int, Guid> tracerIDs = new Dictionary<int, Guid> ();

		public BDDMPSynchronizer ()
		{
			singleton = this;
		}

		public void Awake()
		{
			GameObject.DontDestroyOnLoad (this);

            //Message Registration
            DMPModInterface.fetch.RegisterRawModHandler ("BDDMP:DamageHook", HandleDamageHook);
            DMPModInterface.fetch.RegisterRawModHandler ("BDDMP:BulletHitFXHook", HandleBulletHitFXHook);
            DMPModInterface.fetch.RegisterRawModHandler ("BDDMP:ExplosionFXHook", HandleExplosionFXHook);
            DMPModInterface.fetch.RegisterRawModHandler ("BDDMP:BulletTracerHook", HandleBulletTracerHook);

            //Hook Registration
            HitManager.RegisterHitHook (DamageHook);
            HitManager.RegisterBulletHook (BulletHitFXHook);
            HitManager.RegisterExplosionHook (ExplosionFXHook);
            HitManager.RegisterTracerHook (BulletTracerHook);
            HitManager.RegisterAllowDamageHook (VesselCanBeDamaged);
		}

        public void Update()
        {
            PurgeUpdates ();

            UpdateDamage ();
            UpdateBulletHit ();
            UpdateExplosion ();
            UpdateTracer ();
        }


        #region Update Functions

        private void PurgeUpdates()
        {
            foreach (BDArmoryDamageUpdate update in damageEntriesCompleted) {
                damageEntries.Remove (update);
            }
            foreach (BDArmoryDamageUpdate update in damageEntries) {
                //If update is older than 3 seconds, purge it
                if (Planetarium.GetUniversalTime () - update.entryTime > updateHistoryMinutesToLive * 60) {
                    damageEntries.Remove (update);
                }
            }

            foreach (BDArmoryBulletHitUpdate update in bulletHitEntriesCompleted) {
                bulletHitEntries.Remove (update);
            }
            foreach (BDArmoryBulletHitUpdate update in bulletHitEntries) {
                //If update is older than 3 seconds, purge it
                if (Planetarium.GetUniversalTime () - update.entryTime > updateHistoryMinutesToLive * 60) {
                    bulletHitEntries.Remove (update);
                }
            }

            foreach (BDArmoryExplosionUpdate update in explosionEntriesCompleted) {
                explosionEntries.Remove (update);
            }
            foreach (BDArmoryExplosionUpdate update in explosionEntries) {
                //If update is older than 3 seconds, purge it
                if (Planetarium.GetUniversalTime () - update.entryTime > updateHistoryMinutesToLive * 60) {
                    explosionEntries.Remove (update);
                }
            }

            foreach (BDArmoryTracerUpdate update in tracerEntriesCompleted) {
                tracerEntries.Remove (update);
            }
            foreach (BDArmoryTracerUpdate update in tracerEntries) {
                //If update is older than 3 seconds, purge it
                if (Planetarium.GetUniversalTime () - update.entryTime > updateHistoryMinutesToLive * 60) {
                    tracerEntries.Remove (update);
                }
            }

            damageEntriesCompleted.Clear ();
            bulletHitEntriesCompleted.Clear ();
            explosionEntriesCompleted.Clear ();
            tracerEntriesCompleted.Clear ();
        }

        private void UpdateDamage()
        {
            //Iterate over updates
            foreach (BDArmoryDamageUpdate update in damageEntries) {
                //Don't apply updates till they happen
                if (ApplyUpdate<BDArmoryDamageUpdate> (update)) {
                    foreach (Vessel vessel in FlightGlobals.Vessels) {
                        if (vessel.id == update.vesselID) {
                            foreach (Part part in vessel.Parts) {
                                if (part.flightID == update.flightID) {
                                    part.temperature = update.tempurature;
                                    part.externalTemperature = update.externalTempurature;
                                }
                            }
                        }
                    }
                    damageEntriesCompleted.Add (update);
                }
            }
        }

        private void UpdateBulletHit()
        {
            //Iterate over updates
            foreach (BDArmoryBulletHitUpdate update in bulletHitEntries) {
                //Don't apply updates till they happen
                if (ApplyUpdate<BDArmoryBulletHitUpdate> (update)) {
                    Vector3 relPosition = new Vector3 ();
                    bool positionSet = false;


                    if (FlightGlobals.ActiveVessel.id == update.vesselOriginID) {
                        relPosition = FlightGlobals.ActiveVessel.mainBody.bodyTransform.InverseTransformPoint (update.position);
                        positionSet = true;
                    } else {
                        foreach (Vessel vessel in FlightGlobals.Vessels) {
                            if (vessel.id == update.vesselOriginID) {
                                relPosition = vessel.mainBody.bodyTransform.InverseTransformPoint (update.position);
                                positionSet = true;
                            }
                        }
                    }

                    if (!positionSet) {
                        DarkLog.Debug ("BDDMP Could not find basis vessel!");
                        return;
                    }


                    if (HighLogic.LoadedScene == GameScenes.FLIGHT) {
                        try {
                            //BulletHitFX.CreateBulletHit (relPosition, norm, rico, false);
                            GameObject go = GameDatabase.Instance.GetModel("BDArmory/Models/bulletHit/bulletHit");
                            GameObject newExplosion = (GameObject) GameObject.Instantiate(go, relPosition, Quaternion.LookRotation(update.normalDirection));
                            //Debug.Log ("CreateBulletHit instantiating at position X: " + position.x + " Y: " + position.y + " Z: " + position.z);
                            newExplosion.SetActive(true);
                            newExplosion.AddComponent<BulletHitFX>();
                            newExplosion.GetComponent<BulletHitFX>().ricochet = update.ricochet;
                            foreach(KSPParticleEmitter pe in newExplosion.GetComponentsInChildren<KSPParticleEmitter>())
                            {
                                pe.emit = true;
                                pe.force = (4.49f * FlightGlobals.getGeeForceAtPosition(relPosition));
                            }
                        } catch (Exception e) {
                            DarkLog.Debug ("BDDMP Exception while trying to spawn bullet effect " + e.Message);
                        }
                    }
                    bulletHitEntriesCompleted.Add (update);
                }
            }
        }

        private void UpdateExplosion()
        {
            //Iterate over updates
            foreach (BDArmoryExplosionUpdate update in explosionEntries) {
                //Don't apply updates till they happen
                if (ApplyUpdate<BDArmoryExplosionUpdate> (update)) {
                    if (HighLogic.LoadedScene == GameScenes.FLIGHT) {
                        foreach (Vessel vessel in FlightGlobals.Vessels) {
                            if (vessel.id == update.vesselOriginID) {
                                ExplosionFX.CreateExplosion (vessel.mainBody.bodyTransform.InverseTransformPoint(update.position), update.radius, update.power, vessel, update.direction, update.explModelPath, update.soundPath, false);
                            }
                        }
                    }

                    explosionEntriesCompleted.Add (update);
                }
            }
        }

        private void UpdateTracer()
        {
            //Iterate over updates
            foreach (BDArmoryTracerUpdate update in tracerEntries) {
                //Don't apply updates till they happen
                if (ApplyUpdate<BDArmoryTracerUpdate> (update)) {

                    tracerEntriesCompleted.Add (update);
                }
            }

        }

        #endregion


        #region Network Code
        #region Damage
        void DamageHook(Part hitPart)
        {
            //DarkLog.Debug ("BDDMP Asked to handle HitHook!");
            using (MessageWriter mw = new MessageWriter ()) {
                mw.Write<double> (Planetarium.GetUniversalTime ());
                mw.Write<string> (hitPart.vessel.id.ToString ());
                mw.Write<uint> (hitPart.flightID);
                mw.Write<double> (hitPart.temperature);
                mw.Write<double> (hitPart.externalTemperature);

                DMPModInterface.fetch.SendDMPModMessage("BDDMP:DamageHook", mw.GetMessageBytes(), true, true);
            }
        }

        void HandleDamageHook(byte[] messageData)
        {
            using (MessageReader mr = new MessageReader (messageData)) {
                double timeStamp = mr.Read<double> ();

                Guid vesselID = new Guid (mr.Read<string> ());

                uint partID = mr.Read<uint> ();

                double partTemp = mr.Read<double> ();

                double partTempExt = mr.Read<double> ();

                BDArmoryDamageUpdate update = new BDArmoryDamageUpdate (timeStamp, vesselID, partID, partTemp, partTempExt);

                damageEntries.Add (update);

            }
        }
        #endregion

        #region Bullet Hit FX
        void BulletHitFXHook(BulletObject bullet)
        {
            //Only send per fx tick rate
            bool clearToSend = false || Time.realtimeSinceStartup - lastFXSync > (1f / syncFXHz);
            if (tickCount == syncFXHz && (Time.realtimeSinceStartup - lastFXSync) >= 1) {
                tickCount = 0;
            }

            if (clearToSend) {
                //Set lastFXSync right away
                lastFXSync = Time.realtimeSinceStartup;

                //DarkLog.Debug ("BDDMP Asked to handle BulletHook!");
                using (MessageWriter mw = new MessageWriter ()) {
                    //Get position in world coordinates
                    Vector3 vesselPositionBullet = FlightGlobals.ActiveVessel.mainBody.bodyTransform.TransformPoint (bullet.position);

                    mw.Write<double> (Planetarium.GetUniversalTime ());
                    mw.Write<string> (FlightGlobals.ActiveVessel.id.ToString ());
                    mw.Write<float> (vesselPositionBullet.x);
                    mw.Write<float> (vesselPositionBullet.y);
                    mw.Write<float> (vesselPositionBullet.z);
                    mw.Write<float> (bullet.normalDirection.x);
                    mw.Write<float> (bullet.normalDirection.y);
                    mw.Write<float> (bullet.normalDirection.z);
                    mw.Write<bool> (bullet.ricochet);

                    DMPModInterface.fetch.SendDMPModMessage ("BDDMP:BulletHitFXHook", mw.GetMessageBytes (), true, false);
                }
                tickCount++;
            }
        }

        void HandleBulletHitFXHook(byte[] messageData)
        {
            using (MessageReader mr = new MessageReader(messageData))
            {
                double timeStamp = mr.Read<double> ();

                Guid baseVessel = new Guid (mr.Read<string> ());
                
                float posX = mr.Read<float>();
                float posY = mr.Read<float>();
                float posZ = mr.Read<float>();
                Vector3 pos = new Vector3 (posX, posY, posZ);

                float normX = mr.Read<float>();
                float normY = mr.Read<float>();
                float normZ = mr.Read<float>();
                Vector3 norm = new Vector3 (normX, normY, normZ);

                bool rico = mr.Read<bool> ();

                BDArmoryBulletHitUpdate update = new BDArmoryBulletHitUpdate (timeStamp, baseVessel, pos, norm, rico);
                bulletHitEntries.Add (update);
            }
        }
        #endregion

        #region Explosion FX
        void ExplosionFXHook(ExplosionObject explosion)
        {
            //Reset tickCount at beginning of Hook
            if (tickCount == syncFXHz && (Time.realtimeSinceStartup - lastFXSync) >= 1) {
                tickCount = 0;
            }

            //Only send per fx tick rate
            bool clearToSend = false || Time.realtimeSinceStartup - lastFXSync > (1f / (float)syncFXHz) * (float)tickCount;

            if (clearToSend) {
                //Set lastFXSync and raise tick count right away
                lastFXSync = Time.realtimeSinceStartup;
                tickCount++;

                //DarkLog.Debug ("BDDMP Asked to handle ExplosionHook!");
                using (MessageWriter mw = new MessageWriter ()) {
                    //Get position in world coordinates
                    Vector3 vesselPositionExplosion = explosion.sourceVessel.mainBody.bodyTransform.TransformPoint (explosion.position);

                    mw.Write<double> (Planetarium.GetUniversalTime ());
                    mw.Write<float> (vesselPositionExplosion.x);
                    mw.Write<float> (vesselPositionExplosion.y);
                    mw.Write<float> (vesselPositionExplosion.z);
                    mw.Write<float> (explosion.raduis);
                    mw.Write<float> (explosion.power);
                    mw.Write<string> (explosion.sourceVessel.id.ToString ());
                    mw.Write<float> (explosion.direction.x);
                    mw.Write<float> (explosion.direction.y);
                    mw.Write<float> (explosion.direction.z);
                    mw.Write<string> (explosion.explModelPath);
                    mw.Write<string> (explosion.soundPath);

                    DMPModInterface.fetch.SendDMPModMessage ("BDDMP:ExplosionFXHook", mw.GetMessageBytes (), true, true);
                }
                tickCount++;
            }
        }

        void HandleExplosionFXHook(byte[] messageData)
        {
            using (MessageReader mr = new MessageReader (messageData)) {
                double timeStamp = mr.Read<double> ();

                float posX = mr.Read<float>();
                float posY = mr.Read<float>();
                float posZ = mr.Read<float>();
                Vector3 pos = new Vector3(posX, posY, posZ);

                float radi = mr.Read<float>();

                float power = mr.Read<float> ();

                Guid vesselGUID = new Guid(mr.Read<string>());

                float dirX = mr.Read<float>();
                float dirY = mr.Read<float>();
                float dirZ = mr.Read<float>();
                Vector3 dir = new Vector3 (dirX, dirY, dirZ);

                string explPath = mr.Read<string>();
                string soundPath = mr.Read<string>();

                BDArmoryExplosionUpdate update = new BDArmoryExplosionUpdate (timeStamp, pos, vesselGUID, radi, power, dir, explPath, soundPath);
                explosionEntries.Add (update);

            }
        }
        #endregion

        #region Tracer FX
        void BulletTracerHook(BahaTurretBullet bullet)
        {
            using (MessageWriter mw = new MessageWriter ()) {

                mw.Write<double> (Planetarium.GetUniversalTime ());


                DMPModInterface.fetch.SendDMPModMessage ("BDDMP:BulletTracerHook", mw.GetMessageBytes (), true, true);
            }
        }

        void HandleBulletTracerHook(byte[] messageData)
        {
            using (MessageReader mr = new MessageReader (messageData)) {

            }
        }
        #endregion






        #endregion

        #region Utility Functions
        private bool ApplyUpdate<T> (T entry) where T : BDArmoryUpdate
        {
            double updateDelta = Planetarium.GetUniversalTime () - entry.entryTime;
            if (updateDelta >= 0 && updateDelta < 3 ) {
                return true;
            }
            return false;
        }

        private bool VesselCanBeDamaged(Guid vesselID)
        {
            if (VesselWorker.fetch.LenientVesselUpdatedInFuture (vesselID)) {
                ScreenMessages.PostScreenMessage("BDArmory-DMP: Cannot damage vessel from the past!", 3f, ScreenMessageStyle.UPPER_LEFT);
            }

            return !VesselWorker.fetch.LenientVesselUpdatedInFuture (vesselID);
        }
        #endregion
	}

    public class BDArmoryUpdate
    {
        public double entryTime;
    }
    public class BDArmoryDamageUpdate : BDArmoryUpdate
    {
        public readonly Guid vesselID;
        public readonly uint flightID;
        public readonly double tempurature;
        public readonly double externalTempurature;

        public BDArmoryDamageUpdate(double entryTime, Guid vesselID, uint flightID, double tempurature, double externalTempurature)
        {
            this.entryTime = entryTime;
            this.vesselID = vesselID;
            this.flightID = flightID;
            this.tempurature = tempurature;
            this.externalTempurature = externalTempurature;
        }
    }
    public class BDArmoryBulletHitUpdate : BDArmoryUpdate
    {
        public readonly Guid vesselOriginID;
        public readonly Vector3 position;
        public readonly Vector3 normalDirection;
        public readonly bool ricochet;

        public BDArmoryBulletHitUpdate(double entryTime, Guid vesselOriginID, Vector3 position, Vector3 normalDirection, bool ricochet)
        {
            this.entryTime = entryTime;
            this.vesselOriginID = vesselOriginID;
            this.position = position;
            this.normalDirection = normalDirection;
            this.ricochet = ricochet;
        }
    }
    public class BDArmoryExplosionUpdate : BDArmoryUpdate
    {
        public readonly Vector3 position;
        public readonly Guid vesselOriginID;
        public readonly float radius;
        public readonly float power;
        public readonly Vector3 direction;
        public readonly string explModelPath;
        public readonly string soundPath;

        public BDArmoryExplosionUpdate(double entryTime, Vector3 position, Guid vesselOriginID, float radius, float power, Vector3 direction, string explModelPath, string soundPath)
        {
            this.entryTime = entryTime;
            this.position = position;
            this.vesselOriginID = vesselOriginID;
            this.radius = radius;
            this.power = power;
            this.direction = direction;
            this.explModelPath = explModelPath;
            this.soundPath = soundPath;
        }
    }

    public class BDArmoryTracerUpdate : BDArmoryUpdate
    {
        public readonly Vector3 position;
        public readonly Guid vesselOriginID;
        public readonly Guid objectID;

        public BDArmoryTracerUpdate(double entryTime, Vector3 position, Guid vesselOriginID, Guid objectID)
        {
            this.entryTime = entryTime;
            this.position = position;
            this.vesselOriginID = vesselOriginID;
            this.objectID = objectID;
        }
    }
}

