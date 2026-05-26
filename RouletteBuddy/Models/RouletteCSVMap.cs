using CsvHelper.Configuration;
using RouletteBuddy.DAO;

namespace RouletteBuddy.Models
{
    internal sealed class RouletteCsvMap : ClassMap<Roulette>
    {
        public RouletteCsvMap()
        {
            Map(m => m.RouletteType).Name("任务类型");
            Map(m => m.Date).Name("日期");
            Map(m => m.StartedAt).Name("开始时间");
            Map(m => m.EndedAt).Name("结束时间");
            Map(m => m.GetDurationText(null)).Name("时长");
            Map(m => m.ContentName).Name("任务名称");
            Map(m => m.JobName).Name("职业");
            Map(m => m.IsCompleted).Name("完成情况");
        }
    }
}
