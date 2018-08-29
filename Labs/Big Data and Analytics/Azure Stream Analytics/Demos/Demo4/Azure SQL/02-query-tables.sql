delete from [dbo].[SensorAnalysisOutput];

select * from [dbo].[SensorAnalysisOutput] 
where [SensorId] = 5
order by [OutTime];

