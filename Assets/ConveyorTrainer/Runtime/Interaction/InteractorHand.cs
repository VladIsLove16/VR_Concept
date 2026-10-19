using ConveyorTrainer.Domain;
using UnityEngine;

namespace ConveyorTrainer
{
    public sealed class InteractorHand : MonoBehaviour
    {
        [SerializeField] private Hand hand;
        public Hand Hand => hand;
        public void Configure(Hand side) { hand = side; }
    }
}
