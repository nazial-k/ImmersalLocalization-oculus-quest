﻿/*===============================================================================
Copyright (C) 2024 Immersal Ltd. All Rights Reserved.

This file is part of the Immersal SDK.

The Immersal SDK cannot be copied, distributed, or made available to
third-parties for commercial purposes without written permission of Immersal Ltd.

Contact sdk@immersal.com for licensing requests.
===============================================================================*/

using UnityEngine;
using System;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using Unity.Collections.LowLevel.Unsafe;
using System.Threading.Tasks;
using Immersal.REST;

namespace Immersal.AR
{
    [RequireComponent(typeof(ICameraDataProvider))]
    public class HLLocalizer : LocalizerBase
	{
		private static HLLocalizer instance = null;

		private ICameraDataProvider m_cameraDataProvider;

		private void ARSessionStateChanged(ARSessionStateChangedEventArgs args)
		{
			CheckTrackingState(args.state);
		}

		private void CheckTrackingState(ARSessionState newState)
		{
			isTracking = newState == ARSessionState.SessionTracking;

			if (!isTracking)
			{
				foreach (KeyValuePair<Transform, SpaceContainer> item in ARSpace.transformToSpace)
					item.Value.filter.InvalidateHistory();
			}
		}

		public static HLLocalizer Instance
		{
			get
			{
#if UNITY_EDITOR
				if (instance == null && !Application.isPlaying)
				{
					instance = UnityEngine.Object.FindObjectOfType<HLLocalizer>();
				}
#endif
				if (instance == null)
				{
					Debug.LogError("No HLLocalizer instance found. Ensure one exists in the scene.");
				}
				return instance;
			}
		}

		void Awake()
		{
			if (instance == null)
			{
				instance = this;
			}
			if (instance != this)
			{
				Debug.LogError("There must be only one HLLocalizer object in a scene.");
				UnityEngine.Object.DestroyImmediate(this);
				return;
			}
		}

        public override void Start()
        {
            base.Start();
			m_Sdk.RegisterLocalizer(instance);

            if (m_cameraDataProvider == null)
            {
                m_cameraDataProvider = GetComponent<ICameraDataProvider>();
                if (m_cameraDataProvider == null)
                {
                    Debug.LogError("Could not find Camera Data Provider.");
                    enabled = false;
                }
            }

            if (autoStart)
            {
                m_cameraDataProvider.StartCapture();
            }
		}

		public override void OnEnable()
		{
			base.OnEnable();
#if !UNITY_EDITOR
			CheckTrackingState(ARSession.state);
			ARSession.stateChanged += ARSessionStateChanged;
#endif
		}

		public override void OnDisable()
		{
#if !UNITY_EDITOR
			ARSession.stateChanged -= ARSessionStateChanged;
#endif
			base.OnDisable();
		}

        public override void StartLocalizing()
        {
			m_cameraDataProvider.StartCapture();
            base.StartLocalizing();
        }

        public override void StopLocalizing()
        {
            base.StopLocalizing();
			m_cameraDataProvider.StopCapture();
        }

        public override void Pause()
        {
            base.Pause();
			m_cameraDataProvider.StopCapture();
        }

        public override void OnDestroy()
        {
			m_cameraDataProvider.StopCapture();
            base.OnDestroy();
        }

        public override async void LocalizeServer(SDKMapId[] mapIds)
        {
			byte[] pixels = null;
			int width = 0;
			int height = 0;
			Pose cameraPose = default;
			Vector4 intrinsics = Vector4.zero;
			bool cameraDataIsValid = false;

			if (m_cameraDataProvider != null)
			{
				Task<bool> t = Task.Run(() =>
				{
					return m_cameraDataProvider.TryAcquireLatestData(out pixels, out width, out height, out intrinsics, out cameraPose);
				});

				await t;

				cameraDataIsValid = t.Result && (intrinsics != Vector4.zero);
			}

			if (cameraDataIsValid)
			{
				stats.localizationAttemptCount++;

                JobLocalizeServerAsync j = new JobLocalizeServerAsync();

                Vector3 camPos = cameraPose.position;
                Quaternion camRot = cameraPose.rotation;
                int channels = 1;

				float startTime = Time.realtimeSinceStartup;

                Task<(byte[], CaptureInfo)> t = Task.Run(() =>
                {
                    byte[] capture = new byte[channels * width * height + 8192];
                    CaptureInfo info = Immersal.Core.CaptureImage(capture, capture.Length, pixels, width, height, channels);
                    Array.Resize(ref capture, info.captureSize);
                    return (capture, info);
                });

                await t;

                j.image = t.Result.Item1;
				j.intrinsics = intrinsics;
				j.mapIds = mapIds;

				j.solverType = (int)SolverType;
				float[] rotArray = new float[4];
				if (SolverType == SolverType.Lean)
				{
					Quaternion qRot = m_Cam.transform.rotation;
					ARHelper.GetRotation(ref qRot);
					qRot = ARHelper.SwitchHandedness(qRot);
					rotArray = new float[4] { qRot.x, qRot.y, qRot.z, qRot.w };
				}
				j.camRot = rotArray;

                j.OnResult += (SDKLocalizeResult result) =>
                {
					float elapsedTime = Time.realtimeSinceStartup - startTime;

					if (result.success)
					{
						Debug.Log("*************************** On-Server Localization Succeeded ***************************");
						Debug.Log(string.Format("Relocalized in {0} seconds", elapsedTime));

						int mapId = result.map;

						if (mapId > 0 && ARSpace.mapIdToMap.ContainsKey(mapId))
						{
							ARMap map = ARSpace.mapIdToMap[mapId];

							if (mapId != lastLocalizedMapId)
							{
								if (resetOnMapChange)
								{
									Reset();
								}
								
								lastLocalizedMapId = mapId;

								OnMapChanged?.Invoke(mapId);
							}

							MapOffset mo = ARSpace.mapIdToOffset[mapId];
							stats.localizationSuccessCount++;
							
							Matrix4x4 responseMatrix = Matrix4x4.identity;
							responseMatrix.m00 = result.r00; responseMatrix.m01 = result.r01; responseMatrix.m02 = result.r02; responseMatrix.m03 = result.px;
							responseMatrix.m10 = result.r10; responseMatrix.m11 = result.r11; responseMatrix.m12 = result.r12; responseMatrix.m13 = result.py;
							responseMatrix.m20 = result.r20; responseMatrix.m21 = result.r21; responseMatrix.m22 = result.r22; responseMatrix.m23 = result.pz;
							
							Vector3 pos = responseMatrix.GetColumn(3);
							Quaternion rot = responseMatrix.rotation;
							ARHelper.GetRotation(ref rot);
							pos = ARHelper.SwitchHandedness(pos);
							rot = ARHelper.SwitchHandedness(rot);

							Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.position, mo.rotation, Vector3.one);
							Vector3 scaledPos = Vector3.Scale(pos, mo.scale);
							Matrix4x4 cloudSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, rot, Vector3.one);
							Matrix4x4 trackerSpace = Matrix4x4.TRS(camPos, camRot, Vector3.one);
							Matrix4x4 m = trackerSpace * (cloudSpace.inverse);

							if (useFiltering)
								mo.space.filter.RefinePose(m);
							else
								ARSpace.UpdateSpace(mo.space, m.GetColumn(3), m.rotation);

							double[] ecef = map.MapToEcefGet();
							LocalizerBase.GetLocalizerPose(out lastLocalizedPose, mapId, pos, rot, m.inverse, ecef);
							map.NotifySuccessfulLocalization(mapId);
							OnPoseFound?.Invoke(lastLocalizedPose);
						}
					}
					else
					{
						Debug.Log("*************************** On-Server Localization Failed ***************************");
						Debug.Log(string.Format("Localization attempt failed after {0} seconds", elapsedTime));
					}
                };

				await j.RunJobAsync();
            }

			base.LocalizeServer(mapIds);
        }

        public override async void LocalizeGeoPose(SDKMapId[] mapIds)
        {
			byte[] pixels = null;
			int width = 0;
			int height = 0;
			Pose cameraPose = default;
			Vector4 intrinsics = Vector4.zero;
			bool cameraDataIsValid = false;

			if (m_cameraDataProvider != null)
			{
				Task<bool> t = Task.Run(() =>
				{
					return m_cameraDataProvider.TryAcquireLatestData(out pixels, out width, out height, out intrinsics, out cameraPose);
				});

				await t;

				cameraDataIsValid = t.Result && (intrinsics != Vector4.zero);
			}

			if (cameraDataIsValid)
			{
				stats.localizationAttemptCount++;

                JobGeoPoseAsync j = new JobGeoPoseAsync();

                Vector3 camPos = cameraPose.position;
                Quaternion camRot = cameraPose.rotation;
                int channels = 1;

                j.mapIds = mapIds;
				j.intrinsics = intrinsics;
				j.solverType = (int)SolverType;

				float[] rotArray = new float[4];
				if (SolverType == SolverType.Lean)
				{
					Quaternion qRot = m_Cam.transform.rotation;
					ARHelper.GetRotation(ref qRot);
					qRot = ARHelper.SwitchHandedness(qRot);
					rotArray = new float[4] { qRot.x, qRot.y, qRot.z, qRot.w };
				}
				j.camRot = rotArray;

				float startTime = Time.realtimeSinceStartup;

                Task<(byte[], CaptureInfo)> t = Task.Run(() =>
                {
                    byte[] capture = new byte[channels * width * height + 8192];
                    CaptureInfo info = Immersal.Core.CaptureImage(capture, capture.Length, pixels, width, height, channels);
                    Array.Resize(ref capture, info.captureSize);
                    return (capture, info);
                });

                await t;

                j.image = t.Result.Item1;

                j.OnResult += (SDKGeoPoseResult result) =>
                {
					float elapsedTime = Time.realtimeSinceStartup - startTime;

                    if (result.success)
                    {
                        Debug.Log("*************************** GeoPose Localization Succeeded ***************************");
						Debug.Log(string.Format("Relocalized in {0} seconds", elapsedTime));

                        int mapId = result.map;
                        double latitude = result.latitude;
                        double longitude = result.longitude;
                        double ellipsoidHeight = result.ellipsoidHeight;
                        Quaternion rot = new Quaternion(result.quaternion[1], result.quaternion[2], result.quaternion[3], result.quaternion[0]);
                        Debug.Log(string.Format("GeoPose returned latitude: {0}, longitude: {1}, ellipsoidHeight: {2}, quaternion: {3}", latitude, longitude, ellipsoidHeight, rot));

                        double[] ecef = new double[3];
                        double[] wgs84 = new double[3] { latitude, longitude, ellipsoidHeight };
                        Core.PosWgs84ToEcef(ecef, wgs84);

						if (ARSpace.mapIdToMap.ContainsKey(mapId))
						{
							ARMap map = ARSpace.mapIdToMap[mapId];

							if (mapId != lastLocalizedMapId)
							{
								if (resetOnMapChange)
								{
									Reset();
								}
								
								lastLocalizedMapId = mapId;

								OnMapChanged?.Invoke(mapId);
							}

							MapOffset mo = ARSpace.mapIdToOffset[mapId];
							stats.localizationSuccessCount++;

							double[] mapToEcef = map.MapToEcefGet();
							Vector3 mapPos;
							Quaternion mapRot;
							Core.PosEcefToMap(out mapPos, ecef, mapToEcef);
							Core.RotEcefToMap(out mapRot, rot, mapToEcef);
							ARHelper.GetRotation(ref mapRot);
							mapPos = ARHelper.SwitchHandedness(mapPos);
							mapRot = ARHelper.SwitchHandedness(mapRot);

							Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.position, mo.rotation, Vector3.one);
							Vector3 scaledPos = Vector3.Scale(mapPos, mo.scale);
							Matrix4x4 cloudSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, mapRot, Vector3.one);
							Matrix4x4 trackerSpace = Matrix4x4.TRS(camPos, camRot, Vector3.one);
							Matrix4x4 m = trackerSpace*(cloudSpace.inverse);
							
							if (useFiltering)
								mo.space.filter.RefinePose(m);
							else
								ARSpace.UpdateSpace(mo.space, m.GetColumn(3), m.rotation);

							LocalizerBase.GetLocalizerPose(out lastLocalizedPose, mapId, cloudSpace.GetColumn(3), cloudSpace.rotation, m.inverse, mapToEcef);
							map.NotifySuccessfulLocalization(mapId);
							OnPoseFound?.Invoke(lastLocalizedPose);
						}
                    }
                    else
                    {
                        Debug.Log("*************************** GeoPose Localization Failed ***************************");
						Debug.Log(string.Format("GeoPose localization attempt failed after {0} seconds", elapsedTime));
                    }
                };

                await j.RunJobAsync();
            }

			base.LocalizeGeoPose(mapIds);
        }
 
        public override async void Localize()
		{
			byte[] pixels = null;
			int width = 0;
			int height = 0;
			Pose cameraPose = default;
			Vector4 intrinsics = Vector4.zero;
			bool cameraDataIsValid = false;

			if (m_cameraDataProvider != null)
            {
				Task<bool> t = Task.Run(() =>
				{
					return m_cameraDataProvider.TryAcquireLatestData(out pixels, out width, out height, out intrinsics, out cameraPose);
				});

				await t;

				cameraDataIsValid = t.Result && (intrinsics != Vector4.zero);
            }

			if (cameraDataIsValid)
			{
				stats.localizationAttemptCount++;

				Vector3 camPos = cameraPose.position;
				Quaternion camRot = cameraPose.rotation;

				float[] rotArray = new float[4];
				if (SolverType == SolverType.Lean)
				{
					Quaternion qRot = cameraPose.rotation;
					ARHelper.GetRotation(ref qRot);
					qRot = ARHelper.SwitchHandedness(qRot);
					rotArray = new float[4] { qRot.x, qRot.y, qRot.z, qRot.w };
				}

				unsafe
				{
					ulong handle;
					byte* ptr = (byte*)UnsafeUtility.PinGCArrayAndGetDataAddress(pixels, out handle);
					m_PixelBuffer = (IntPtr)ptr;
					UnsafeUtility.ReleaseGCObject(handle);
				}

				if (m_PixelBuffer != IntPtr.Zero)
				{
					float startTime = Time.realtimeSinceStartup;

					Task<LocalizeInfo> t = Task.Run(() =>
					{
						if (SolverType == SolverType.Lean)
							return Immersal.Core.LocalizeImage(width, height, ref intrinsics, m_PixelBuffer, rotArray);
						
						return Immersal.Core.LocalizeImage(width, height, ref intrinsics, m_PixelBuffer);
					});

					await t;

					LocalizeInfo locInfo = t.Result;

					Matrix4x4 resultMatrix = Matrix4x4.identity;
					resultMatrix.m00 = locInfo.r00; resultMatrix.m01 = locInfo.r01; resultMatrix.m02 = locInfo.r02; resultMatrix.m03 = locInfo.px;
					resultMatrix.m10 = locInfo.r10; resultMatrix.m11 = locInfo.r11; resultMatrix.m12 = locInfo.r12; resultMatrix.m13 = locInfo.py;
					resultMatrix.m20 = locInfo.r20; resultMatrix.m21 = locInfo.r21; resultMatrix.m22 = locInfo.r22; resultMatrix.m23 = locInfo.pz;

					Vector3 pos = resultMatrix.GetColumn(3);
					Quaternion rot = resultMatrix.rotation;

					int mapHandle = locInfo.handle;
					int mapId = ARMap.MapHandleToId(mapHandle);
					float elapsedTime = Time.realtimeSinceStartup - startTime;

					if (mapId > 0 && ARSpace.mapIdToOffset.ContainsKey(mapId))
					{
						ARHelper.GetRotation(ref rot);
						pos = ARHelper.SwitchHandedness(pos);
						rot = ARHelper.SwitchHandedness(rot);

						Debug.Log(string.Format("Relocalized in {0} seconds", elapsedTime));
						stats.localizationSuccessCount++;

						if (mapId != lastLocalizedMapId)
						{
							if (resetOnMapChange)
							{
								Reset();
							}
							
							lastLocalizedMapId = mapId;

							OnMapChanged?.Invoke(mapId);
						}
						
						MapOffset mo = ARSpace.mapIdToOffset[mapId];
						Matrix4x4 offsetNoScale = Matrix4x4.TRS(mo.position, mo.rotation, Vector3.one);
						Vector3 scaledPos = Vector3.Scale(pos, mo.scale);
						Matrix4x4 cloudSpace = offsetNoScale * Matrix4x4.TRS(scaledPos, rot, Vector3.one);
						Matrix4x4 trackerSpace = Matrix4x4.TRS(camPos, camRot, Vector3.one);
						Matrix4x4 m = trackerSpace * (cloudSpace.inverse);

						if (useFiltering)
							mo.space.filter.RefinePose(m);
						else
							ARSpace.UpdateSpace(mo.space, m.GetColumn(3), m.rotation);

						GetLocalizerPose(out lastLocalizedPose, mapId, pos, rot, m.inverse);
						OnPoseFound?.Invoke(lastLocalizedPose);

						ARMap map = ARSpace.mapIdToMap[mapId];
						map.NotifySuccessfulLocalization(mapId);
					}
					else
					{
						Debug.Log(string.Format("Localization attempt failed after {0} seconds", elapsedTime));
					}
				}
			}

			base.Localize();
		}
    }
}