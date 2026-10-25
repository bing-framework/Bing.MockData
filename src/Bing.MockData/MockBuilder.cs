using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Bing.MockData
{



    /// <summary>
    /// 按配置的规则顺序创建并填充目标对象。
    /// </summary>
    /// <remarks>
    /// 未配置成员保留构造结果；创建生成器时会保存独立规则快照。
    /// </remarks>
    public sealed class MockBuilder<T> where T : class
    {
        /// <summary>
        /// 保存此构建器关联的上下文。
        /// </summary>
        private readonly MockContext _context;
        /// <summary>
        /// 按声明顺序保存成员规则。
        /// </summary>
        private readonly List<Rule> _rules = new List<Rule>();
        private readonly List<Action<MockContext,T>> _validators = new List<Action<MockContext,T>>();
        /// <summary>在所有字段赋值后检查跨字段约束。验证委托随 Build 冻结。</summary>
        public MockBuilder<T> ValidateWith(Action<MockContext,T> validation)
        { _validators.Add(validation ?? throw new ArgumentNullException(nameof(validation))); return this; }
        /// <summary>
        /// 保存被显式忽略的成员名称。
        /// </summary>
        private readonly HashSet<string> _ignored = new HashSet<string>(StringComparer.Ordinal);
        /// <summary>
        /// 保存可选的对象构造委托。
        /// </summary>
        private Func<MockContext, T> _constructor;
        /// <summary>
        /// 指示是否要求为所有公开可写成员配置规则。
        /// </summary>
        private bool _strict;




        /// <summary>
        /// 创建与指定上下文关联的对象构建器。 上下文用于规则求值、重放、取消和预算检查。 构建器不会取得或释放上下文的所有权。
        /// </summary>
        /// <param name="context">用于生成对象的上下文。</param>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <see langword="null"/>。</exception>
        public MockBuilder(MockContext context) { _context = context ?? throw new ArgumentNullException(nameof(context)); }




        /// <summary>
        /// 设置创建目标对象的委托。 未设置时使用目标类型的公开无参构造函数。 委托返回 <see langword="null"/> 时，生成该对象会失败。
        /// </summary>
        /// <param name="constructor">接收当前字段上下文并创建对象的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="constructor"/> 为 <see langword="null"/>。</exception>
        public MockBuilder<T> ConstructUsing(Func<MockContext, T> constructor)
        { _constructor = constructor ?? throw new ArgumentNullException(nameof(constructor)); return this; }




        /// <summary>
        /// 启用或关闭严格成员校验。 启用后，未配置且未忽略的公开可写属性和字段会导致校验失败。 校验在调用 <see cref="Validate"/> 或 <see cref="Build"/> 时执行。
        /// </summary>
        /// <param name="enabled"><see langword="true"/> 表示启用严格校验；默认为 <see langword="true"/>。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        public MockBuilder<T> Strict(bool enabled = true) { _strict = enabled; return this; }




        /// <summary>
        /// 为目标成员添加生成规则。 表达式必须直接引用目标对象的公开可写属性或字段。 规则按添加顺序执行，重复配置的成员会被拒绝。
        /// </summary>
        /// <typeparam name="TValue">目标成员的类型。</typeparam>
        /// <param name="member">指向目标成员的直接成员表达式。</param>
        /// <param name="generate">根据当前字段上下文生成成员值的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> RuleFor<TValue>(Expression<Func<T, TValue>> member, Func<MockContext, TValue> generate)
        { if (generate == null) throw new ArgumentNullException(nameof(generate)); return RuleFor(member, (c, _) => generate(c)); }




        /// <summary>
        /// 为目标成员添加生成规则。 委托可读取当前正在填充的对象以及该成员的字段上下文。 规则按添加顺序执行，重复配置的成员会被拒绝。
        /// </summary>
        /// <typeparam name="TValue">目标成员的类型。</typeparam>
        /// <param name="member">指向目标成员的直接成员表达式。</param>
        /// <param name="generate">根据字段上下文和当前对象生成成员值的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> RuleFor<TValue>(Expression<Func<T, TValue>> member, Func<MockContext, T, TValue> generate)
            => SetRule(member, generate, false);




        /// <summary>
        /// 显式添加或替换目标成员的生成规则。 替换时保留原规则在执行顺序中的位置；尚无规则时追加到末尾。 被忽略的成员不能通过此方法重新配置。
        /// </summary>
        /// <typeparam name="TValue">目标成员的类型。</typeparam>
        /// <param name="member">指向目标成员的直接成员表达式。</param>
        /// <param name="generate">根据字段上下文和当前对象生成成员值的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已被忽略。</exception>
        public MockBuilder<T> Override<TValue>(Expression<Func<T, TValue>> member, Func<MockContext, T, TValue> generate)
            => SetRule(member, generate, true);




        /// <summary>
        /// 添加或替换成员规则的内部实现。 编译成员赋值器，并在每次生成时检查值的文本和集合预算。 替换已有规则时保留其原有执行位置。
        /// </summary>
        /// <typeparam name="TValue">目标成员的类型。</typeparam>
        /// <param name="member">指向目标成员的直接成员表达式。</param>
        /// <param name="generate">生成成员值的委托。</param>
        /// <param name="replace"><see langword="true"/> 表示允许替换已有规则。</param>
        /// <returns>当前构建器。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="generate"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，成员已配置且不允许替换，或成员已忽略。</exception>
        private MockBuilder<T> SetRule<TValue>(Expression<Func<T, TValue>> member, Func<MockContext, T, TValue> generate, bool replace)
        {
            if (generate == null) throw new ArgumentNullException(nameof(generate)); var expression = Member(member);
            string name = expression.Member.Name; int index = _rules.FindIndex(r => r.Name == name);
            if ((!replace && index >= 0) || _ignored.Contains(name)) throw new ArgumentException("成员已配置或忽略：" + name, nameof(member));
            var value = Expression.Parameter(typeof(TValue), "value");
            var setter = Expression.Lambda<Action<T, TValue>>(Expression.Assign(expression, value), member.Parameters[0], value).Compile();
            var rule = new Rule(name, (c, target) => { var result = generate(c, target); ValidateValue(c, result); setter(target, result); });
            if (index >= 0) _rules[index] = rule; else _rules.Add(rule); return this;
        }




        /// <summary>
        /// 将目标成员标记为不由规则填充。 严格模式下，被忽略的成员不要求配置规则。 已配置或已忽略的成员不能重复忽略。
        /// </summary>
        /// <typeparam name="TValue">目标成员的类型。</typeparam>
        /// <param name="member">指向目标成员的直接成员表达式。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> Ignore<TValue>(Expression<Func<T, TValue>> member)
        {
            string name = Member(member).Member.Name;
            if (_rules.Any(r => r.Name == name) || !_ignored.Add(name)) throw new ArgumentException("成员已配置或忽略：" + name, nameof(member));
            return this;
        }




        /// <summary>
        /// 将子对象构建规则嵌入当前成员。 调用时立即捕获子构建器的规则快照，之后对子构建器的修改不会生效。 子对象生成时继续使用当前上下文的深度、重放和预算信息。
        /// </summary>
        /// <typeparam name="TChild">子对象的引用类型。</typeparam>
        /// <param name="member">指向目标子对象成员的直接成员表达式。</param>
        /// <param name="child">用于生成子对象的构建器。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="child"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> Nested<TChild>(Expression<Func<T, TChild>> member, MockBuilder<TChild> child) where TChild : class
        { if (child == null) throw new ArgumentNullException(nameof(child)); var recipe = child.Build(); return RuleFor(member, c => recipe.Evaluate(c)); }




        /// <summary>
        /// 为只读列表成员配置批量子项生成规则。 每次生成会在闭区间数量范围内创建子项，并返回只读列表包装。 生成数量受上下文的最大集合大小预算约束。
        /// </summary>
        /// <typeparam name="TItem">列表元素的类型。</typeparam>
        /// <param name="member">指向只读列表成员的直接成员表达式。</param>
        /// <param name="min">每次生成的最小项数，包含此值。</param>
        /// <param name="max">每次生成的最大项数，包含此值。</param>
        /// <param name="item">根据每个子项的字段上下文创建元素的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> 小于零，或 <paramref name="max"/> 小于 <paramref name="min"/>。</exception>
        /// <exception cref="MockLimitException">最大项数超过上下文集合预算。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> Collection<TItem>(Expression<Func<T, IReadOnlyList<TItem>>> member, int min, int max, Func<MockContext, TItem> item)
        { ValidateCollection(min, max, item); return RuleFor(member, c => (IReadOnlyList<TItem>)Items(c, min, max, item).AsReadOnly()); }




        /// <summary>
        /// 为可变列表成员配置批量子项生成规则。 每次生成会在闭区间数量范围内创建子项，并返回新建的可变列表。 生成数量受上下文的最大集合大小预算约束。
        /// </summary>
        /// <typeparam name="TItem">列表元素的类型。</typeparam>
        /// <param name="member">指向可变列表成员的直接成员表达式。</param>
        /// <param name="min">每次生成的最小项数，包含此值。</param>
        /// <param name="max">每次生成的最大项数，包含此值。</param>
        /// <param name="item">根据每个子项的字段上下文创建元素的委托。</param>
        /// <returns>当前构建器，便于继续配置。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> 小于零，或 <paramref name="max"/> 小于 <paramref name="min"/>。</exception>
        /// <exception cref="MockLimitException">最大项数超过上下文集合预算。</exception>
        /// <exception cref="ArgumentException">成员表达式无效，或成员已配置、已忽略。</exception>
        public MockBuilder<T> Collection<TItem>(Expression<Func<T, List<TItem>>> member, int min, int max, Func<MockContext, TItem> item)
        { ValidateCollection(min, max, item); return RuleFor(member, c => Items(c, min, max, item)); }




        /// <summary>
        /// 检查集合生成参数及其上下文预算。 验证发生在添加规则时，实际数量仍会在每次生成时抽取。 该方法不创建集合，也不调用元素工厂。
        /// </summary>
        /// <typeparam name="TItem">集合元素的类型。</typeparam>
        /// <param name="min">允许的最小项数。</param>
        /// <param name="max">允许的最大项数。</param>
        /// <param name="item">元素生成委托。</param>
        /// <exception cref="ArgumentNullException"><paramref name="item"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="min"/> 小于零，或 <paramref name="max"/> 小于 <paramref name="min"/>。</exception>
        /// <exception cref="MockLimitException"><paramref name="max"/> 超过上下文集合预算。</exception>
        private void ValidateCollection<TItem>(int min, int max, Func<MockContext, TItem> item)
        {
            if (item == null) throw new ArgumentNullException(nameof(item));
            if (min < 0 || max < min) throw new ArgumentOutOfRangeException(nameof(min));
            if (max > _context.Budget.MaxCollectionSize) throw new MockLimitException(nameof(MockLimits.MaxCollectionSize), _context.Budget.MaxCollectionSize);
        }




        /// <summary>
        /// 按抽取的数量创建集合元素。 每个元素使用带有索引字段路径的子上下文并执行取消、预算检查。 元素工厂中的已知生成异常会原样传播，其他异常会包装为生成异常。
        /// </summary>
        /// <typeparam name="TItem">集合元素的类型。</typeparam>
        /// <param name="context">用于抽取数量和创建字段上下文的上下文。</param>
        /// <param name="min">最小项数，包含此值。</param>
        /// <param name="max">最大项数，包含此值。</param>
        /// <param name="item">创建每个元素的委托。</param>
        /// <returns>新建的可变列表。</returns>
        /// <exception cref="MockLimitException">抽取的项数超过上下文集合预算。</exception>
        /// <exception cref="MockGenerationException">元素工厂抛出未被专门传播的异常。</exception>
        /// <exception cref="OperationCanceledException">上下文在生成过程中被取消。</exception>
        private static List<TItem> Items<TItem>(MockContext context, int min, int max, Func<MockContext, TItem> item)
        {
            int count = context.Numbers.Int(min, max);
            if (count > context.Budget.MaxCollectionSize) throw new MockLimitException(nameof(MockLimits.MaxCollectionSize), context.Budget.MaxCollectionSize);
            var result = new List<TItem>(count);
            for (int i = 0; i < count; i++)
            {
                var child = context.ForField("[" + i.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]"); child.Check();
                try { var value = item(child); ValidateValue(child, value); result.Add(value); }
                catch (OperationCanceledException) { throw; } catch (MockGenerationException) { throw; } catch (MockLimitException) { throw; }
                catch (Exception e) { throw new MockGenerationException("collection", child, e); }
            }
            return result;
        }




        /// <summary>
        /// 检查构建器当前配置是否有效。 检查上下文状态、对象构造方式和严格模式下的成员覆盖情况。 此方法只验证配置，不生成目标对象。
        /// </summary>
        /// <exception cref="InvalidOperationException">类型缺少公开无参构造函数，或严格模式存在未配置成员。</exception>
        /// <exception cref="ObjectDisposedException">关联的上下文已释放。</exception>
        public void Validate()
        {
            _context.Check();
            if (_constructor == null && (typeof(T).IsAbstract || typeof(T).GetConstructor(Type.EmptyTypes) == null))
                throw new InvalidOperationException(typeof(T).Name + " 没有公开无参构造函数，请使用 ConstructUsing。");
            if (_strict)
            {
                var writable = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(p => p.SetMethod != null && p.SetMethod.IsPublic && p.GetIndexParameters().Length == 0).Select(p => p.Name)
                    .Concat(typeof(T).GetFields(BindingFlags.Instance | BindingFlags.Public).Where(f => !f.IsInitOnly).Select(f => f.Name));
                var missing = writable.Where(n => !_ignored.Contains(n) && !_rules.Any(r => r.Name == n)).ToArray();
                if (missing.Length > 0) throw new InvalidOperationException("严格模式缺少规则：" + string.Join(", ", missing));
            }
        }




        /// <summary>
        /// 创建使用当前规则快照的对象生成器。 每条记录先创建对象，再按规则声明顺序设置成员。 之后修改此构建器不会改变已返回生成器的规则。
        /// </summary>
        /// <returns>可重复生成对象的生成器。</returns>
        /// <exception cref="InvalidOperationException"><see cref="Validate"/> 检查发现配置无效。</exception>
        /// <exception cref="ObjectDisposedException">关联的上下文已释放。</exception>
        public MockGenerator<T> Build()
        {
            Validate(); var constructor = _constructor ?? (_ => Activator.CreateInstance<T>()); var rules = _rules.ToArray(); var validators = _validators.ToArray();
            return _context.Generator(c =>
            {
                var current = c.EnterObject(); T target;
                try { target = constructor(current.ForField("$constructor")) ?? throw new InvalidOperationException("构造函数返回 null。"); }
                catch (OperationCanceledException) { throw; } catch (MockGenerationException) { throw; } catch (MockLimitException) { throw; }
                catch (Exception e) { throw new MockGenerationException("constructor", current.ForField("$constructor"), e); }
                foreach (var rule in rules)
                {
                    var field = current.ForField(rule.Name); field.Check();
                    try { rule.Apply(field, target); }
                    catch (OperationCanceledException) { throw; } catch (MockGenerationException) { throw; } catch (MockLimitException) { throw; }
                    catch (Exception e) { throw new MockGenerationException(rule.Name, field, e); }
                }
                foreach (var validate in validators) { current.Check(); try { validate(current, target); } catch (OperationCanceledException) { throw; } catch (MockLimitException) { throw; } catch (Exception e) { throw new MockGenerationException("validation", current, e); } }
                return target;
            }, typeof(T).Name);
        }




        /// <summary>
        /// 在指定的字段上下文中立即生成一个对象。 此入口用于递归或嵌套规则，以继续沿用传入上下文的深度和重放参数。 每次调用都会基于当前构建器配置创建生成器并求值一次。
        /// </summary>
        /// <param name="context">用于本次生成的字段上下文。</param>
        /// <returns>生成的对象实例。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="context"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="MockGenerationException">构造委托或成员规则抛出未被专门传播的异常。</exception>
        /// <exception cref="MockLimitException">生成过程超过配置的预算。</exception>
        /// <exception cref="OperationCanceledException">生成过程被取消。</exception>
        public T Generate(MockContext context) => Build().Evaluate(context ?? throw new ArgumentNullException(nameof(context)));




        /// <summary>
        /// 从表达式中解析目标类型的直接成员。 仅接受当前对象的公开可写实例属性或非只读公开实例字段。 不支持索引器、静态成员和嵌套成员访问。
        /// </summary>
        /// <typeparam name="TValue">成员的类型。</typeparam>
        /// <param name="expression">待解析的成员表达式。</param>
        /// <returns>解析出的成员表达式。</returns>
        /// <exception cref="ArgumentNullException"><paramref name="expression"/> 为 <see langword="null"/>。</exception>
        /// <exception cref="ArgumentException">表达式不是受支持的直接成员表达式，或成员不可写。</exception>
        private static MemberExpression Member<TValue>(Expression<Func<T, TValue>> expression)
        {
            if (expression == null) throw new ArgumentNullException(nameof(expression));
            if (!(expression.Body is MemberExpression member) || member.Expression != expression.Parameters[0]) throw new ArgumentException("仅支持直接实例成员。", nameof(expression));
            if (member.Member is PropertyInfo property && property.SetMethod != null && property.SetMethod.IsPublic && property.GetIndexParameters().Length == 0) return member;
            if (member.Member is FieldInfo field && field.IsPublic && !field.IsInitOnly) return member;
            throw new ArgumentException("成员必须公开可写。", nameof(expression));
        }




        /// <summary>
        /// 对成员值执行上下文预算检查。 当前检查字符串长度以及实现了非泛型 <see cref="System.Collections.ICollection"/> 的集合数量。 此方法不复制或释放传入值。
        /// </summary>
        /// <typeparam name="TValue">成员值的静态类型。</typeparam>
        /// <param name="context">提供预算配置的字段上下文。</param>
        /// <param name="value">待检查的成员值。</param>
        /// <exception cref="MockLimitException">字符串或受支持集合超过相应预算。</exception>
        private static void ValidateValue<TValue>(MockContext context, TValue value)
        {
            if (value is string text && text.Length > context.Budget.MaxTextLength) throw new MockLimitException(nameof(MockLimits.MaxTextLength), context.Budget.MaxTextLength);
            int count = 0;
            if (value is System.Collections.ICollection collection) count = collection.Count;
            else if (!(value is string) && value is System.Collections.IEnumerable)
            {
                var contract = value.GetType().GetInterfaces().FirstOrDefault(type => type.IsGenericType &&
                    (type.GetGenericTypeDefinition() == typeof(IReadOnlyCollection<>) || type.GetGenericTypeDefinition() == typeof(ICollection<>)));
                if (contract != null) count = (int)contract.GetProperty("Count").GetValue(value);
            }
            if (count > context.Budget.MaxCollectionSize) throw new MockLimitException(nameof(MockLimits.MaxCollectionSize), context.Budget.MaxCollectionSize);
        }




        /// <summary>
        /// 保存一个已编译的成员赋值规则。
        /// </summary>
        /// <remarks>
        /// 规则接收当前字段上下文和正在填充的目标对象，由所属构建器内部创建和执行。
        /// </remarks>
        private sealed class Rule
        {
            /// <summary>
            /// 获取该规则所针对的成员名称。
            /// </summary>
            internal readonly string Name;
            /// <summary>
            /// 获取执行成员值生成和赋值的委托。
            /// </summary>
            internal readonly Action<MockContext, T> Apply;




            /// <summary>
            /// 创建成员规则。 调用方应提供非空名称和赋值委托。 参数由构建器在规则解析阶段验证。
            /// </summary>
            /// <param name="name">目标成员名称。</param>
            /// <param name="apply">执行规则的委托。</param>
            internal Rule(string name, Action<MockContext, T> apply) { Name = name; Apply = apply; }
        }
    }
    public sealed partial class MockContext
    {



        /// <summary>
        /// 创建与当前上下文关联的对象构建器。 构建器生成时使用此上下文的种子、预算、取消和重放状态。 返回的构建器不会取得当前上下文的释放所有权。
        /// </summary>
        /// <typeparam name="T">要生成的引用类型。</typeparam>
        /// <returns>用于配置目标对象生成规则的构建器。</returns>
        /// <exception cref="ObjectDisposedException">当前上下文已释放。</exception>
        public MockBuilder<T> Builder<T>() where T : class { Check(); return new MockBuilder<T>(this); }
    }
}
