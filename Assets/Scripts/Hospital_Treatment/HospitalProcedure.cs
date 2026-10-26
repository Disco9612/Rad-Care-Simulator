using System;
using System.Collections.Generic;

namespace RadCare.HospitalTreatment
{
    public enum HospitalAction { Start, Chart, AskPain, RemoveDressing, DisposeDressing, AdjustLight,
        MeasureWound, MeasureSkin, MeasureReference, RecordBefore, PlaceDrape, PlaceReceiver, SelectSterile,
        WashFirst, RespondPain, WashSecond, ClearWaste, RecordAfter, ReviewOrder, ProtectChoice,
        ApplyDressing, SecureDressing, ExplainPlan, SaveChart }

    // Pure progression model: no input or scene dependency. Rejected actions never advance time or steps.
    public sealed class HospitalProcedure
    {
        public sealed class Step
        {
            public readonly int Stage;
            public readonly HospitalAction Action;
            public readonly string Prompt;
            public Step(int stage, HospitalAction action, string prompt) { Stage=stage; Action=action; Prompt=prompt; }
        }
        public static readonly string[] Stages={"환자 인계", "상처 노출·평가", "처치 부위 준비", "상처 정밀 세척", "재평가·치료 판단", "상처 보호", "기록·종료"};
        public static readonly string[] Windows={"0:00–0:30", "0:30–1:10", "1:10–1:40", "1:40–2:40", "2:40–3:30", "3:30–4:10", "4:10–4:30"};
        public static readonly Step[] Steps={
            new Step(0,HospitalAction.Start,"전자차트의 ‘교육 시작’을 선택하세요."),
            new Step(0,HospitalAction.Chart,"전자차트에서 환자 ID와 현장 처치 이력을 확인하세요."),
            new Step(0,HospitalAction.AskPain,"환자에게 현재 통증을 질문하세요."),
            new Step(1,HospitalAction.RemoveDressing,"기존 드레싱을 집으세요."),
            new Step(1,HospitalAction.DisposeDressing,"기존 드레싱을 지정 폐기함에 놓으세요."),
            new Step(1,HospitalAction.AdjustLight,"처치등의 ‘상처 방향 조절’을 선택하세요."),
            new Step(1,HospitalAction.MeasureWound,"보조자에게 상처·주변 피부·대조 부위 측정을 요청하고 보고를 확인하세요."),
            new Step(1,HospitalAction.MeasureSkin,"주변 피부 측정 지점을 선택하세요."),
            new Step(1,HospitalAction.MeasureReference,"대조 부위 측정 지점을 선택하세요."),
            new Step(1,HospitalAction.RecordBefore,"세 부위의 처치 전 측정값을 차트에 기록하세요."),
            new Step(2,HospitalAction.PlaceDrape,"방수포를 집어 상처 주변의 파란 배치 지점에 놓으세요."),
            new Step(2,HospitalAction.PlaceReceiver,"체액 받이를 집어 상처 아래의 받이 지점에 놓으세요."),
            new Step(2,HospitalAction.SelectSterile,"준비된 멸균 세척 도구를 집으세요."),
            new Step(3,HospitalAction.WashFirst,"주사기 분출구를 발광 상처 부위에 대고 트리거를 누르세요. 유효 세척 10초."),
            new Step(3,HospitalAction.RespondPain,"주사기를 발광하는 카트의 원래 위치에 내려놓은 뒤, 모니터에서 환자 상태를 확인하세요."),
            new Step(3,HospitalAction.WashSecond,"카트의 주사기를 다시 집고 분출구를 발광 상처 부위에 대어 세척을 재개하세요. 유효 세척 10초."),
            new Step(4,HospitalAction.ClearWaste,"세척 도구를 원래 카트 위치에 놓고, 오염된 소모품을 집어 지정 폐기함 안에 넣으세요."),
            new Step(4,HospitalAction.MeasureWound,"보조자에게 세척 후 재측정을 요청하고 보고를 확인하세요."),
            new Step(4,HospitalAction.MeasureSkin,"주변 피부를 다시 측정하세요."),
            new Step(4,HospitalAction.MeasureReference,"대조 부위를 다시 측정하세요."),
            new Step(4,HospitalAction.RecordAfter,"처치 전후 측정값을 비교·기록하세요."),
            new Step(5,HospitalAction.ApplyDressing,"새 드레싱을 집어 상처의 드레싱 지점에 놓으세요."),
            new Step(5,HospitalAction.SecureDressing,"카트의 테이프를 집어 발광하는 드레싱 위에 가져다 대고 잠시 유지하세요."),
            new Step(5,HospitalAction.ExplainPlan,"환자에게 이후 관찰·보고 계획을 설명하세요."),
            new Step(6,HospitalAction.SaveChart,"부위·전후 결과·환자 반응·후속 계획을 확인하고 기록을 저장하세요.")
        };
        [Serializable] public sealed class Entry { public string action; public int stage; public float seconds; }
        public int Index { get; private set; }
        public bool Complete => Index >= Steps.Length;
        public bool Started => Index > 0;
        public Step Current => Complete ? null : Steps[Index];
        public float Elapsed { get; private set; }
        public float WashSeconds { get; private set; }
        public readonly List<Entry> Entries=new List<Entry>();
        public bool IsWashing => !Complete && (Current.Action==HospitalAction.WashFirst || Current.Action==HospitalAction.WashSecond);
        public void Tick(float seconds) { if(Started&&!Complete&&seconds>0)Elapsed+=seconds; }
        public bool Try(HospitalAction action) {
            if(Complete||Current.Action!=action||IsWashing)return false;
            Advance();return true;
        }
        public bool Irrigate(float seconds,bool valid) {
            if(!IsWashing||!valid||seconds<=0)return false;
            WashSeconds=Math.Min(10,WashSeconds+seconds);
            if(WashSeconds<10)return false;
            Advance();return true;
        }
        void Advance() {
            Entries.Add(new Entry { action=Current.Action.ToString(),stage=Current.Stage,seconds=Elapsed });
            Index++;WashSeconds=0;
        }
    }
}
