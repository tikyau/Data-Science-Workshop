drop table if exists [dbo].[SensorAnalysisOutput];
create table [dbo].[SensorAnalysisOutput]
(
  [SensorId] int ,
  [OutTime] datetime2(7) ,
  [MinOilTemperature] float ,
  [AvgOilTemperature] float ,
  [MaxOilTemperature] float ,
  --[Result] nvarchar(max) check(isjson([Result])=1),
  [ScoredLabel] nvarchar(100)
);

create clustered index [IXC] on [dbo].[SensorAnalysisOutput] ([OutTime]);
go
