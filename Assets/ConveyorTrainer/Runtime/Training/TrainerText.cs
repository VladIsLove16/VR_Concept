using ConveyorTrainer.Domain;

namespace ConveyorTrainer
{
    public static class TrainerText
    {
        public static string Failure(Failure reason, Operation operation = Operation.SensorNut)
        {
            switch (reason)
            {
                case Domain.Failure.LineRunning: return "Линия работает. Обслуживание запрещено.";
                case Domain.Failure.WrongTool: return operation == Operation.SensorNut || operation == Operation.PusherBolt
                    ? "Для этого крепежа нужен подходящий инструмент в этой руке."
                    : "Этой рукой удерживается инструмент. Для действия используйте свободную руку.";
                case Domain.Failure.EquipmentMissing: return "Оборудование не установлено.";
                case Domain.Failure.EquipmentLoose: return "Оборудование должно быть установлено, закреплено и подключено.";
                case Domain.Failure.CableConnected: return "Кабель датчика подключён.";
                case Domain.Failure.TubesConnected: return "Трубки толкателя подключены.";
                case Domain.Failure.FastenersTight: return "Не весь крепёж освобождён.";
                case Domain.Failure.AirSupplyOpen: return "Подача воздуха открыта.";
                case Domain.Failure.ResidualPressure: return "В системе осталось давление.";
                case Domain.Failure.HandOccupied: return "Эта рука уже занята предметом.";
                case Domain.Failure.NotHeld: return "Предмет не находится в этой руке.";
                case Domain.Failure.SocketOccupied: return "Место установки занято.";
                case Domain.Failure.WrongSocket: return "Предмет не подходит к этому месту установки.";
                case Domain.Failure.InvalidPlacement: return "Отпустите предмет в обозначенном месте. Предмет возвращён.";
                default: return "Действие выполнено.";
            }
        }
        public static string Information(InfoTarget target)
        {
            switch (target)
            {
                case InfoTarget.Conveyor: return "КОНВЕЙЕР\nПеремещает шайбы к датчику. При остановке все шайбы удаляются.";
                case InfoTarget.Sensor: return "ДАТЧИК\nРаспознаёт металлические шайбы. Жёлтая лампа означает обнаружение металла; команда толкателю выполняется с задержкой.";
                default: return "ТОЛКАТЕЛЬ\nИспользует сжатый воздух и физически направляет металлическую шайбу в ящик. Перед обслуживанием перекройте подачу и стравите давление.";
            }
        }
    }
}
