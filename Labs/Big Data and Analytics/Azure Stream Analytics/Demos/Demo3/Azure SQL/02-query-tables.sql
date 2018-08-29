select * from dbo.TumblingStream where Category = 'TV' and [Type] = 'Silver' order by WindowStart;
select * from dbo.HoppingStream where Category = 'TV' and [Type] = 'Silver' order by WindowStart;
select * from dbo.SlidingStream where Category = 'TV' and [Type] = 'Silver' order by WindowStart;
