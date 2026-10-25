using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Bing.MockData.Internals.Generators;

namespace Bing.MockData
{
    /// <summary>配置一次的属性约束生成器。显式规则优先，所有输出仍经过 DataAnnotations 验证。</summary>
    public sealed class AutoMockBuilder<T> where T:class
    {
        private readonly MockContext _context;
        private readonly Dictionary<string,Func<MockContext,T,object>> _rules=new Dictionary<string,Func<MockContext,T,object>>(StringComparer.Ordinal);
        private readonly HashSet<string> _ignored=new HashSet<string>(StringComparer.Ordinal);
        private Func<MockContext,T> _constructor;
        private readonly Type[] _typePath;
        private readonly int _planDepth;
        internal AutoMockBuilder(MockContext context,Type[] ancestors=null,int planDepth=0)
        {
            _context=context;_planDepth=planDepth;
            var path=ancestors??System.Array.Empty<Type>();
            if(path.Contains(typeof(T)))throw new InvalidOperationException("自动类型包含循环："+string.Join(" -> ",path.Concat(new[]{typeof(T)}).Select(x=>x.Name))+"；请显式配置该字段或注册生成器。");
            _typePath=path.Concat(new[]{typeof(T)}).ToArray();
        }
        public AutoMockBuilder<T> ConstructUsing(Func<MockContext,T> constructor){_constructor=constructor??throw new ArgumentNullException(nameof(constructor));return this;}
        public AutoMockBuilder<T> RuleFor<TValue>(Expression<Func<T,TValue>> expression,Func<MockContext,TValue> generate)
        {if(generate==null)throw new ArgumentNullException(nameof(generate));return RuleFor(expression,(c,_)=>generate(c));}
        public AutoMockBuilder<T> RuleFor<TValue>(Expression<Func<T,TValue>> expression,Func<MockContext,T,TValue> generate)
        {var p=Property(expression);if(generate==null)throw new ArgumentNullException(nameof(generate));if(_ignored.Contains(p.Name)||_rules.ContainsKey(p.Name))throw new ArgumentException("重复规则："+p.Name);_rules.Add(p.Name,(c,x)=>generate(c,x));return this;}
        public AutoMockBuilder<T> Ignore<TValue>(Expression<Func<T,TValue>> expression)
        {var p=Property(expression);if(_rules.ContainsKey(p.Name)||!_ignored.Add(p.Name))throw new ArgumentException("重复规则："+p.Name);return this;}
        private static PropertyInfo Property<TValue>(Expression<Func<T,TValue>> expression)
        {if(!(expression?.Body is MemberExpression m)||m.Expression!=expression.Parameters[0]||!(m.Member is PropertyInfo p)||p.SetMethod?.IsPublic!=true)throw new ArgumentException("需要公开可写直接属性。");return p;}
        public MockGenerator<T> Build()
        {
            _context.Check();if(_constructor==null&&typeof(T).GetConstructor(Type.EmptyTypes)==null)throw new InvalidOperationException(typeof(T).FullName+" 需要显式 ConstructUsing。");
            var ctor=_constructor??(_=>Activator.CreateInstance<T>());
            var properties=typeof(T).GetProperties(BindingFlags.Instance|BindingFlags.Public).Where(p=>p.SetMethod?.IsPublic==true&&p.GetIndexParameters().Length==0).OrderBy(p=>p.Name,StringComparer.Ordinal).ToArray();
            var getters=properties.ToDictionary(p=>p.Name,p=>Getter(p),StringComparer.Ordinal);
            var setters=properties.ToDictionary(p=>p.Name,p=>Setter(p),StringComparer.Ordinal);
            var attributesByName=properties.ToDictionary(p=>p.Name,p=>p.GetCustomAttributes<ValidationAttribute>(true).ToArray(),StringComparer.Ordinal);
            var actions=new List<Action<MockContext,T>>();var comparisons=new List<Action<T>>();var compareLinks=new Dictionary<string,PropertyInfo>(StringComparer.Ordinal);
            foreach(var p in properties)
            {
                if(_ignored.Contains(p.Name))continue;
                if(_rules.TryGetValue(p.Name,out var explicitRule)){actions.Add((c,x)=>setters[p.Name](x,explicitRule(c.ForField(p.Name),x)));continue;}
                var attributes=attributesByName[p.Name];
                var compare=attributes.OfType<CompareAttribute>().SingleOrDefault();
                if(compare!=null){var other=typeof(T).GetProperty(compare.OtherProperty);if(other==null||!p.PropertyType.IsAssignableFrom(other.PropertyType))throw new InvalidOperationException(typeof(T).Name+"."+p.Name+": Compare 无效。");compareLinks.Add(p.Name,other);continue;}
                Func<MockContext,object> generate=Compile(p.PropertyType,attributes,_context,typeof(T).Name+"."+p.Name,_planDepth);
                actions.Add((c,x)=>
                {
                    var field=c.ForField(p.Name);
                    try { setters[p.Name](x,generate(field)); }
                    catch(OperationCanceledException){throw;}catch(MockLimitException){throw;}catch(MockGenerationException){throw;}
                    catch(Exception error){throw new MockGenerationException("DataAnnotations:"+typeof(T).Name+"."+p.Name,field,error);}
                });
            }
            var ordered=new HashSet<string>(StringComparer.Ordinal);var pending=new HashSet<string>(StringComparer.Ordinal);
            void Schedule(string name)
            {
                if(ordered.Contains(name))return;
                if(!pending.Add(name))throw new InvalidOperationException(typeof(T).Name+"."+name+": Compare 循环依赖。");
                var other=compareLinks[name];if(compareLinks.ContainsKey(other.Name))Schedule(other.Name);
                var getOther=getters.TryGetValue(other.Name,out var cached)?cached:Getter(other);comparisons.Add(x=>setters[name](x,getOther(x)));pending.Remove(name);ordered.Add(name);
            }
            foreach(var name in compareLinks.Keys)Schedule(name);
            return _context.Generator(c=>
            {
                var nested=c.EnterObject();List<ValidationResult> results=null;T last=null;
                for(int attempt=0;attempt<c.Budget.MaxAttempts;attempt++)
                {
                    var current=nested.ForField("attempt["+attempt+"]");var instance=ctor(current.ForField("$constructor"))??throw new InvalidOperationException("构造器返回 null。");
                    foreach(var action in actions){current.Check();action(current,instance);}foreach(var compare in comparisons){current.Check();compare(instance);}
                    foreach(var getter in getters.Values)
                    {var value=getter(instance);if(value is string text&&text.Length>c.Budget.MaxTextLength)throw new MockLimitException(nameof(MockLimits.MaxTextLength),c.Budget.MaxTextLength);if(value is System.Collections.ICollection collection)DomainGuard.Count(c,collection.Count);}
                    last=instance;results=new List<ValidationResult>();if(Validator.TryValidateObject(instance,new ValidationContext(instance),results,true))return instance;
                }
                string member=results.SelectMany(r=>r.MemberNames).FirstOrDefault()??"$object";
                string constraint="Validation";
                if(attributesByName.TryGetValue(member,out var attrs))
                {
                    var validation=new ValidationContext(last){MemberName=member};
                    constraint=attrs.FirstOrDefault(a=>a.GetValidationResult(getters[member](last),validation)!=ValidationResult.Success)?.GetType().Name??constraint;
                }
                throw new MockGenerationException("DataAnnotations:"+typeof(T).FullName+"."+member+":"+constraint,c.ForField(member),
                    new ValidationException("在尝试预算内未满足约束："+string.Join("; ",results.Select(r=>string.Join(",",r.MemberNames)+": "+r.ErrorMessage))));
            },"DataAnnotations:"+typeof(T).FullName);
        }
        private static Func<T,object> Getter(PropertyInfo property)
        {
            var target=Expression.Parameter(typeof(T),"target");
            return Expression.Lambda<Func<T,object>>(Expression.Convert(Expression.Property(target,property),typeof(object)),target).Compile();
        }
        private static Action<T,object> Setter(PropertyInfo property)
        {
            var target=Expression.Parameter(typeof(T),"target");var value=Expression.Parameter(typeof(object),"value");
            return Expression.Lambda<Action<T,object>>(Expression.Assign(Expression.Property(target,property),Expression.Convert(value,property.PropertyType)),target,value).Compile();
        }
        private Func<MockContext,object> Compile(Type propertyType,ValidationAttribute[] a,MockContext context,string member,int depth=0)
        {
            if(depth>context.Budget.MaxDepth)throw new MockLimitException(nameof(MockLimits.MaxDepth),context.Budget.MaxDepth);
            Type type=Nullable.GetUnderlyingType(propertyType)??propertyType;
            if(context.TryRegisteredFactory(propertyType,out var registered))return registered;
            var enumConstraint=a.OfType<EnumDataTypeAttribute>().SingleOrDefault();
            if(enumConstraint!=null)
            {
                if(!enumConstraint.EnumType.IsEnum)throw new InvalidOperationException(member+": EnumDataType 需要枚举。");
                var values=Enum.GetValues(enumConstraint.EnumType).Cast<object>().ToArray();
                return c=>{var value=c.Collections.Pick(values);return type==typeof(string)?value.ToString():type.IsEnum?Enum.ToObject(type,value):Convert.ChangeType(value,type,CultureInfo.InvariantCulture);};
            }
            if(type==typeof(string))
            {
                int min=a.OfType<RequiredAttribute>().Any(x=>!x.AllowEmptyStrings)?1:0,max=context.Budget.MaxTextLength;bool hasMaximum=false;
                foreach(var x in a.OfType<StringLengthAttribute>()){min=Math.Max(min,x.MinimumLength);max=Math.Min(max,x.MaximumLength);hasMaximum=true;}
                foreach(var x in a.OfType<MinLengthAttribute>())min=Math.Max(min,x.Length);
                foreach(var x in a.OfType<MaxLengthAttribute>()){max=Math.Min(max,x.Length);hasMaximum=true;}
                if(!hasMaximum)max=Math.Min(context.Budget.MaxTextLength,Math.Max(min,32));
                if(min>max||min<0)throw new InvalidOperationException(member+": StringLength/MinLength/MaxLength 约束矛盾。");
                if(max>context.Budget.MaxTextLength)max=context.Budget.MaxTextLength;
                if(min>max)throw new MockLimitException(nameof(MockLimits.MaxTextLength),context.Budget.MaxTextLength);
                var regex=a.OfType<RegularExpressionAttribute>().SingleOrDefault();
                var regexGenerator=regex==null?null:new BoundedRegexGenerator(regex.Pattern,context.Budget.MaxTextLength,true);
                return c=>
                {
                    if(regexGenerator!=null)return c.WithRandom(random=>regexGenerator.Generate(random,c.CancellationToken,min,max,c.Budget.MaxAttempts));
                    if(a.Any(x=>x is EmailAddressAttribute))return FormatText(c,"email",min,max);
                    if(a.Any(x=>x is UrlAttribute))return FormatText(c,"url",min,max);
                    if(a.Any(x=>x is PhoneAttribute))return FormatText(c,"phone",min,max);
                    if(a.Any(x=>x is CreditCardAttribute))return FormatText(c,"card",min,max);
                    return c.Text.Random(min,max);
                };
            }
            if(type==typeof(char))return c=>(object)c.Text.Random(1,1,"ABCDEFGHIJKLMNOPQRSTUVWXYZ")[0];
            if(type==typeof(bool))return c=>c.Numbers.Boolean();
            if(type==typeof(Guid))return c=>c.Network.Guid();
            if(type.IsEnum){var values=Enum.GetValues(type).Cast<object>().ToArray();return c=>c.Collections.Pick(values);}
            var enumType=a.OfType<EnumDataTypeAttribute>().SingleOrDefault();
            if(enumType!=null){var values=Enum.GetValues(enumType.EnumType).Cast<object>().ToArray();return c=>Convert.ChangeType(c.Collections.Pick(values),type,CultureInfo.InvariantCulture);}
            var range=a.OfType<RangeAttribute>().SingleOrDefault();
            if(type==typeof(DateTime)){DateTime min=range==null?context.ReferenceTime.UtcDateTime.AddYears(-1):Convert.ToDateTime(range.Minimum,CultureInfo.InvariantCulture),max=range==null?context.ReferenceTime.UtcDateTime:Convert.ToDateTime(range.Maximum,CultureInfo.InvariantCulture);if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");return c=>c.Dates.Between(min,max);}
            if(type==typeof(DateTimeOffset))
            {
                var min=range==null?context.ReferenceTime.AddYears(-1):DateTimeOffset.Parse(Convert.ToString(range.Minimum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                var max=range==null?context.ReferenceTime:DateTimeOffset.Parse(Convert.ToString(range.Maximum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");return c=>c.Dates.Between(min,max);
            }
#if NET10_0_OR_GREATER
            if(type==typeof(DateOnly))
            {
                var min=range==null?DateOnly.FromDateTime(context.ReferenceTime.AddYears(-1).DateTime):DateOnly.Parse(Convert.ToString(range.Minimum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                var max=range==null?DateOnly.FromDateTime(context.ReferenceTime.DateTime):DateOnly.Parse(Convert.ToString(range.Maximum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");return c=>(object)DateOnly.FromDayNumber(c.Numbers.Int(min.DayNumber,max.DayNumber));
            }
            if(type==typeof(TimeOnly))
            {
                var min=range==null?TimeOnly.MinValue:TimeOnly.Parse(Convert.ToString(range.Minimum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                var max=range==null?TimeOnly.MaxValue:TimeOnly.Parse(Convert.ToString(range.Maximum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");return c=>(object)new TimeOnly(c.Numbers.Long(min.Ticks,max.Ticks));
            }
#endif
            if(type==typeof(TimeSpan))
            {
                var min=range==null?TimeSpan.Zero:TimeSpan.Parse(Convert.ToString(range.Minimum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                var max=range==null?TimeSpan.FromDays(1):TimeSpan.Parse(Convert.ToString(range.Maximum,CultureInfo.InvariantCulture),CultureInfo.InvariantCulture);
                if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");return c=>TimeSpan.FromTicks(c.Numbers.Long(min.Ticks,max.Ticks));
            }
            if(type==typeof(double)||type==typeof(float))
            {
                double min=range==null?0:Convert.ToDouble(range.Minimum,CultureInfo.InvariantCulture),max=range==null?100:Convert.ToDouble(range.Maximum,CultureInfo.InvariantCulture);
                if(type==typeof(float)){min=Math.Max(min,-float.MaxValue);max=Math.Min(max,float.MaxValue);}
                if(double.IsNaN(min)||double.IsInfinity(min)||double.IsNaN(max)||double.IsInfinity(max)||min>max)throw new InvalidOperationException(member+": Range 约束矛盾或非有限。");
                return c=>Convert.ChangeType(c.Numbers.Double(min,max),type,CultureInfo.InvariantCulture);
            }
            if(type.IsPrimitive||type==typeof(decimal))
            {
                decimal min=range==null?0:Convert.ToDecimal(range.Minimum,CultureInfo.InvariantCulture),max=range==null?100:Convert.ToDecimal(range.Maximum,CultureInfo.InvariantCulture);
                bool integral=type!=typeof(float)&&type!=typeof(double)&&type!=typeof(decimal);
                if(integral){min=decimal.Ceiling(min);max=decimal.Floor(max);var minimum=type.GetField("MinValue");var maximum=type.GetField("MaxValue");if(minimum!=null)min=Math.Max(min,Convert.ToDecimal(minimum.GetValue(null),CultureInfo.InvariantCulture));if(maximum!=null)max=Math.Min(max,Convert.ToDecimal(maximum.GetValue(null),CultureInfo.InvariantCulture));}if(min>max)throw new InvalidOperationException(member+": Range 约束矛盾。");
                int precision=integral?0:Math.Max(2,Math.Max((decimal.GetBits(min)[3]>>16)&255,(decimal.GetBits(max)[3]>>16)&255));
                return c=>Convert.ChangeType(c.Numbers.Decimal(min,max,precision),type,CultureInfo.InvariantCulture);
            }
            bool list=type.IsGenericType&&new[]{typeof(List<>),typeof(IList<>),typeof(ICollection<>),typeof(IReadOnlyList<>),typeof(IEnumerable<>)}.Contains(type.GetGenericTypeDefinition());
            bool set=type.IsGenericType&&new[]{typeof(HashSet<>),typeof(ISet<>)}.Contains(type.GetGenericTypeDefinition());
            bool dictionary=type.IsGenericType&&new[]{typeof(Dictionary<,>),typeof(IDictionary<,>),typeof(IReadOnlyDictionary<,>)}.Contains(type.GetGenericTypeDefinition());
            if(type.IsArray||list||set||dictionary)
            {
                if(type.IsArray&&type.GetArrayRank()!=1)throw new InvalidOperationException(member+": 仅支持一维数组；其他类型请显式配置规则。");
                int min=a.OfType<MinLengthAttribute>().Select(x=>x.Length).DefaultIfEmpty(0).Max();
                int max=a.OfType<MaxLengthAttribute>().Select(x=>x.Length).DefaultIfEmpty(Math.Max(min,3)).Min();
                if(min<0||min>max)throw new InvalidOperationException(member+": MinLength/MaxLength 集合长度约束矛盾。");
                DomainGuard.Count(context,max);Type item=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];
                var generateItem=Compile(item,System.Array.Empty<ValidationAttribute>(),context,member+"[]",depth+1);
                if(dictionary)
                {
                    var arguments=type.GetGenericArguments();var value=Compile(arguments[1],System.Array.Empty<ValidationAttribute>(),context,member+"[value]",depth+1);
                    return InvokePlan("DictionaryPlan",arguments,new object[]{generateItem,value,min,max});
                }
                if(set)return InvokePlan("SetPlan",new[]{item},new object[]{generateItem,min,max});
                var listType=typeof(List<>).MakeGenericType(item);
                return c=>
                {
                    var nested=c.EnterObject();int count=c.Numbers.Int(min,max);
                    var result=System.Array.CreateInstance(item,count);
                    for(int i=0;i<count;i++){var field=nested.ForField("["+i+"]");field.Check();result.SetValue(generateItem(field),i);}
                    if(type.IsArray)return result;
                    var target=(System.Collections.IList)Activator.CreateInstance(listType);
                    foreach(var value in result)target.Add(value);return target;
                };
            }
            if(type.IsClass&&!type.IsAbstract&&type.GetConstructor(Type.EmptyTypes)!=null)
                return InvokePlan("NestedPlan",new[]{type},new object[]{context,_typePath,depth+1});
            return context.RegisteredFactory(type);
        }
        private static Func<MockContext,object> InvokePlan(string name,Type[] arguments,object[] values)
        {
            try{return (Func<MockContext,object>)typeof(AutoMockBuilder<T>).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).MakeGenericMethod(arguments).Invoke(null,values);}
            catch(TargetInvocationException error) when(error.InnerException!=null){System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw();throw;}
        }
        private static Func<MockContext,object> NestedPlan<TNested>(MockContext context,Type[] ancestors,int depth) where TNested:class
        {
            if(depth>context.Budget.MaxDepth)throw new MockLimitException(nameof(MockLimits.MaxDepth),context.Budget.MaxDepth);
            var generator=new AutoMockBuilder<TNested>(context,ancestors,depth).Build();return c=>generator.Evaluate(c);
        }
        private static Func<MockContext,object> SetPlan<TItem>(Func<MockContext,object> item,int min,int max)
        {
            return c=>
            {
                var nested=c.EnterObject();var result=new HashSet<TItem>();int count=c.Numbers.Int(min,max);
                for(int i=0;i<count;i++)
                {
                    bool added=false;
                    for(int attempt=0;attempt<c.Budget.MaxAttempts&&!added;attempt++){var field=nested.ForField("["+i+"].attempt["+attempt+"]");field.Check();added=result.Add((TItem)item(field));}
                    if(!added)throw new InvalidOperationException("集合候选耗尽或达到唯一值重试预算。");
                }
                return result;
            };
        }
        private static Func<MockContext,object> DictionaryPlan<TKey,TValue>(Func<MockContext,object> key,Func<MockContext,object> value,int min,int max)
        {
            return c=>
            {
                var nested=c.EnterObject();var result=new Dictionary<TKey,TValue>();int count=c.Numbers.Int(min,max);
                for(int i=0;i<count;i++)
                {
                    bool added=false;
                    for(int attempt=0;attempt<c.Budget.MaxAttempts&&!added;attempt++)
                    {
                        var field=nested.ForField("["+i+"].attempt["+attempt+"]");field.Check();var candidate=(TKey)key(field.ForField("key"));
                        if((object)candidate==null||result.ContainsKey(candidate))continue;
                        result.Add(candidate,(TValue)value(field.ForField("value")));added=true;
                    }
                    if(!added)throw new InvalidOperationException("字典键候选耗尽或达到唯一值重试预算。");
                }
                return result;
            };
        }
        private static string FormatText(MockContext context,string format,int min,int max)
        {
            const string letters="abcdefghijklmnopqrstuvwxyz";
            if(format=="email")
            {
                // Keep example.com normally; shorter constraints use the reserved .test suffix.
                string suffix=max>=13?"@example.com":max>=8?"@x.test":"@x";
                int lower=Math.Max(1,min-suffix.Length),upper=Math.Min(64,max-suffix.Length);
                if(lower>upper)throw new ValidationException("EmailAddress 与长度约束不兼容。");
                int length=context.Numbers.Int(lower,Math.Min(upper,Math.Max(lower,12)));
                return context.Text.Random(length,length,letters)+suffix;
            }
            if(format=="url")
            {
                string prefix=max>=20?"https://example.com/":"http://x/";
                if(max<prefix.Length)throw new ValidationException("Url 与长度约束不兼容。");
                int lower=Math.Max(0,min-prefix.Length),upper=Math.Min(max-prefix.Length,Math.Max(lower,12));
                return prefix+context.Text.Random(lower,upper,letters);
            }
            if(format=="phone")
            {
                int lower=Math.Max(1,min),upper=Math.Min(max,Math.Max(lower,12));
                if(lower>upper)throw new ValidationException("Phone 与长度约束不兼容。");
                return context.Text.Random(lower,upper,"0123456789");
            }
            int low=Math.Max(12,min),high=Math.Min(19,max);
            if(low>high)throw new ValidationException("CreditCard 与长度约束不兼容。");
            int digits=context.Numbers.Int(low,high);return Luhn(context.Text.Random(digits-1,digits-1,"0123456789"));
        }
        internal static string Luhn(string body)
        {int sum=0;bool twice=true;for(int i=body.Length-1;i>=0;i--){int x=body[i]-'0';if(twice){x*=2;if(x>9)x-=9;}sum+=x;twice=!twice;}return body+(10-sum%10)%10;}
    }
    public sealed partial class MockContext
    {
        public AutoMockBuilder<T> AutoBuilder<T>() where T:class {Check();return new AutoMockBuilder<T>(this);}
        internal Func<MockContext,object> RegisteredFactory(Type type)
        {
            if(!TryRegisteredFactory(type,out var factory))throw new InvalidOperationException("类型需要唯一的注册生成器："+type.FullName);return factory;
        }
        internal bool TryRegisteredFactory(Type type,out Func<MockContext,object> factory)
        {
            Check();var registrations=_state.Registrations.Values.Where(x=>x.Type==type).ToArray();
            if(registrations.Length>1)throw new InvalidOperationException("类型需要唯一的注册生成器："+type.FullName);
            factory=registrations.Length==0?null:registrations[0].Factory;return factory!=null;
        }
    }
}
