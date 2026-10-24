using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;

namespace ConveyorTrainer.Tests
{
    public sealed class ControllerLayoutTests
    {
        [TestCase(typeof(Pico4ControllerProfile), "TrainerPico4Controller", "PICO 4 Controller OpenXR")]
        [TestCase(typeof(ViveFocus3ControllerProfile), "TrainerViveFocus3Controller", "VIVE Focus 3 Controller OpenXR")]
        public void RegistrationMatchesControllerAndCanBeRepeated(Type profileType, string layoutName, string product)
        {
            var profile = ScriptableObject.CreateInstance(profileType);
            var register = typeof(ThumbstickControllerProfile).GetMethod("RegisterDeviceLayout",
                BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                // OpenXR registers layouts both on initialization and on the first Editor frame.
                for (var pass = 0; pass < 2; pass++)
                {
                    Assert.DoesNotThrow(() => register.Invoke(profile, null));
                    Assert.That(InputSystem.LoadLayout(layoutName), Is.Not.Null);
                    Assert.That(InputSystem.TryFindMatchingLayout(new InputDeviceDescription
                    {
                        interfaceName = "XRInputV1",
                        product = product
                    }), Is.EqualTo(layoutName));
                }
            }
            finally
            {
                InputSystem.RemoveLayout(layoutName);
                UnityEngine.Object.DestroyImmediate(profile);
            }
        }
    }
}
