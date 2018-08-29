drop table if exists [dbo].[TumblingStream];
create table [dbo].[TumblingStream]
(
  [Category] nvarchar(100) not null ,
  [Type] nvarchar(100) not null ,
  [SoldQuantity] decimal(18) null ,
  [WindowStart] datetime2(7) not null ,
  [WindowEnd] datetime2(7) not null
);
create clustered index [IXC] on [dbo].[TumblingStream] ([WindowStart]);
go

drop table if exists [dbo].[HoppingStream];
create table [dbo].[HoppingStream]
(
  [Category] nvarchar(100) ,
  [Type] nvarchar(100) ,
  [SoldQuantity] decimal(18) ,
  [WindowStart] datetime2(7) ,
  [WindowEnd] datetime2(7)
);

create clustered index [IXC] on [dbo].[HoppingStream] ([WindowStart]);
go

drop table if exists [dbo].[SlidingStream];
create table [dbo].[SlidingStream]
(
  [Category] nvarchar(100) ,
  [Type] nvarchar(100) ,
  [SoldQuantity] decimal(18) ,
  [WindowStart] datetime2(7) ,
  [WindowEnd] datetime2(7)
);

create clustered index [IXC] on [dbo].[SlidingStream] ([WindowStart]);
go

