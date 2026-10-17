using System.Reflection;
using CsCheck;
using Trail.Domain.Common;
using Trail.Domain.Planning;

namespace Trail.Domain.Tests.Planning;

public sealed class SubjectTests
{
    private readonly UserId _ownerId = UserId.New();
    private readonly StudyPlanId _planId = StudyPlanId.New();

    [Fact]
    public void Create_WithValidData_ReturnsSubject()
    {
        var result = Subject.Create(_ownerId, _planId, "Direito Constitucional");

        Assert.True(result.IsOk);
        var subject = result.Data;
        Assert.Equal("Direito Constitucional", subject.Name);
        Assert.Equal(_ownerId, subject.OwnerId);
        Assert.Equal(_planId, subject.StudyPlanId);
        Assert.Empty(subject.Topics);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyName_ReturnsInvalid(string? name)
    {
        var result = Subject.Create(_ownerId, _planId, name);

        Assert.False(result.IsOk);
        Assert.Equal("planning.subject.name_required", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Create_WithNameExceedingMaxLength_ReturnsInvalid()
    {
        var longName = new string('s', Subject.MaxNameLength + 1);
        var result = Subject.Create(_ownerId, _planId, longName);

        Assert.False(result.IsOk);
        Assert.Equal("planning.subject.name_too_long", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void Rename_WithValidName_UpdatesName()
    {
        var subject = Subject.Create(_ownerId, _planId, "Direito Administrativo").Data;

        var result = subject.Rename("Direito Constitucional");

        Assert.True(result.IsOk);
        Assert.Equal("Direito Constitucional", subject.Name);
    }

    [Fact]
    public void AddTopic_RootLevel_AssignsOrderAndSearchText()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var code = TopicCode.Create("1").Data;

        var t1 = subject.AddTopic("Segurança da Informação", code).Data;
        var t2 = subject.AddTopic("Redes de Computadores").Data;

        Assert.Equal(0, t1.Order);
        Assert.Null(t1.ParentId);
        Assert.Equal("1 seguranca da informacao", t1.SearchText);
        Assert.Equal(1, t2.Order);
        Assert.Null(t2.Code);
        Assert.Equal("redes de computadores", t2.SearchText);
    }

    [Fact]
    public void AddTopic_WithParent_PlacesUnderParent()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var parent = subject.AddTopic("Segurança da Informação").Data;
        var child = subject.AddTopic("Criptografia", parentId: parent.Id).Data;

        Assert.Equal(parent.Id, child.ParentId);
        Assert.Equal(0, child.Order);

        var missingParentResult = subject.AddTopic("Malware", parentId: TopicId.New());
        Assert.False(missingParentResult.IsOk);
        Assert.Equal("planning.topic.parent_not_found", missingParentResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void AddTopic_DuplicateCode_ReturnsConflict()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var code = TopicCode.Create("1.1").Data;

        subject.AddTopic("Tópico A", code);
        var result = subject.AddTopic("Tópico B", code);

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.duplicate_code", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void UpdateTopic_WithDuplicateCodeOnOtherTopic_ReturnsConflict()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var code1 = TopicCode.Create("1").Data;
        var code2 = TopicCode.Create("2").Data;

        var t1 = subject.AddTopic("Tópico 1", code1).Data;
        var t2 = subject.AddTopic("Tópico 2", code2).Data;

        var result = subject.UpdateTopic(t2.Id, "Tópico 2 Atualizado", code1);

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.duplicate_code", result.Failure.Messages[0].Code);

        var sameResult = subject.UpdateTopic(t1.Id, "Tópico 1 Novo Título", code1);
        Assert.True(sameResult.IsOk);
    }

    [Fact]
    public void MoveTopic_PreventsCycleAtAnyDepth()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var a = subject.AddTopic("A").Data;
        var b = subject.AddTopic("B", parentId: a.Id).Data;
        var c = subject.AddTopic("C", parentId: b.Id).Data;
        var d = subject.AddTopic("D", parentId: c.Id).Data;

        var cycleSelf = subject.MoveTopic(a.Id, a.Id);
        Assert.False(cycleSelf.IsOk);
        Assert.Equal("planning.topic.cycle_detected", cycleSelf.Failure.Messages[0].Code);

        var cycleDirect = subject.MoveTopic(a.Id, b.Id);
        Assert.False(cycleDirect.IsOk);
        Assert.Equal("planning.topic.cycle_detected", cycleDirect.Failure.Messages[0].Code);

        var cycleDeep = subject.MoveTopic(a.Id, d.Id);
        Assert.False(cycleDeep.IsOk);
        Assert.Equal("planning.topic.cycle_detected", cycleDeep.Failure.Messages[0].Code);

        var validMove = subject.MoveTopic(d.Id, a.Id);
        Assert.True(validMove.IsOk);
        Assert.Equal(a.Id, subject.Topics.First(t => t.Id == d.Id).ParentId);
    }

    [Fact]
    public void ReorderTopics_UpdatesOrderCorrectly()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var t1 = subject.AddTopic("T1").Data;
        var t2 = subject.AddTopic("T2").Data;
        var t3 = subject.AddTopic("T3").Data;

        var result = subject.ReorderTopics(null, [t3.Id, t1.Id, t2.Id]);

        Assert.True(result.IsOk);
        Assert.Equal(t3.Id, subject.Topics[0].Id);
        Assert.Equal(t1.Id, subject.Topics[1].Id);
        Assert.Equal(t2.Id, subject.Topics[2].Id);
    }

    [Fact]
    public void ChangeTopicStatus_RaisesTopicStatusChangedEventOnlyOnActualChange()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var topic = subject.AddTopic("Tópico").Data;

        var result = subject.ChangeTopicStatus(topic.Id, TopicStatus.Studying);

        Assert.True(result.IsOk);
        var events = subject.PullDomainEvents();
        var domainEvent = Assert.Single(events);
        var statusChanged = Assert.IsType<TopicStatusChanged>(domainEvent);
        Assert.Equal(subject.Id, statusChanged.SubjectId);
        Assert.Equal(topic.Id, statusChanged.TopicId);
        Assert.Equal(TopicStatus.ToStudy, statusChanged.PreviousStatus);
        Assert.Equal(TopicStatus.Studying, statusChanged.NewStatus);

        var noChangeResult = subject.ChangeTopicStatus(topic.Id, TopicStatus.Studying);
        Assert.True(noChangeResult.IsOk);
        Assert.Empty(subject.PullDomainEvents());
    }

    [Fact]
    public void RemoveTopicSubtree_RemovesTopicAndAllDescendantsAndNormalizesSiblings()
    {
        var subject = Subject.Create(_ownerId, _planId, "Informática").Data;
        var a = subject.AddTopic("A").Data;
        var b = subject.AddTopic("B").Data;
        var a1 = subject.AddTopic("A1", parentId: a.Id).Data;
        var a2 = subject.AddTopic("A2", parentId: a.Id).Data;
        var a1Sub = subject.AddTopic("A1.1", parentId: a1.Id).Data;

        var removeResult = subject.RemoveTopicSubtree(a.Id);

        Assert.True(removeResult.IsOk);
        var removedIds = removeResult.Data;
        Assert.Equal(4, removedIds.Count);
        Assert.Contains(a.Id, removedIds);
        Assert.Contains(a1.Id, removedIds);
        Assert.Contains(a2.Id, removedIds);
        Assert.Contains(a1Sub.Id, removedIds);

        var remaining = Assert.Single(subject.Topics);
        Assert.Equal(b.Id, remaining.Id);
        Assert.Equal(0, remaining.Order);
    }

    [Fact]
    public void Property_CycleDetectionRejectsAnyDescendantAtArbitraryDepth()
    {
        Gen.Int[2, 8].Sample(depth =>
        {
            var subject = Subject.Create(_ownerId, _planId, "Subject").Data;
            var topics = new List<TopicId>();

            TopicId? currentParent = null;
            for (var i = 0; i < depth; i++)
            {
                var topic = subject.AddTopic($"Topic {i}", parentId: currentParent).Data;
                topics.Add(topic.Id);
                currentParent = topic.Id;
            }

            for (var ancestorIndex = 0; ancestorIndex < topics.Count - 1; ancestorIndex++)
            {
                for (var descendantIndex = ancestorIndex + 1; descendantIndex < topics.Count; descendantIndex++)
                {
                    var moveResult = subject.MoveTopic(topics[ancestorIndex], topics[descendantIndex]);
                    Assert.False(moveResult.IsOk);
                    Assert.Equal("planning.topic.cycle_detected", moveResult.Failure.Messages[0].Code);
                }
            }
        });
    }

    [Fact]
    public void ParameterlessConstructors_InitializeDefaults()
    {
        var subjectCtor = typeof(Subject).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(subjectCtor);
        var subject = (Subject)subjectCtor.Invoke(null);
        Assert.Equal(string.Empty, subject.Name);
        Assert.Empty(subject.Topics);

        var topicCtor = typeof(Topic).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            null,
            Type.EmptyTypes,
            null);
        Assert.NotNull(topicCtor);
        var topic = (Topic)topicCtor.Invoke(null);
        Assert.Equal(string.Empty, topic.Title);
        Assert.Equal(string.Empty, topic.SearchText);
    }

    [Fact]
    public void Rename_WithInvalidName_ReturnsInvalid()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;

        var emptyResult = subject.Rename("   ");
        Assert.False(emptyResult.IsOk);
        Assert.Equal("planning.subject.name_required", emptyResult.Failure.Messages[0].Code);

        var longName = new string('s', Subject.MaxNameLength + 1);
        var longResult = subject.Rename(longName);
        Assert.False(longResult.IsOk);
        Assert.Equal("planning.subject.name_too_long", longResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void AddTopic_WithInvalidTitle_ReturnsInvalid()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;

        var emptyResult = subject.AddTopic("   ");
        Assert.False(emptyResult.IsOk);
        Assert.Equal("planning.topic.title_required", emptyResult.Failure.Messages[0].Code);

        var longTitle = new string('t', Subject.MaxTopicTitleLength + 1);
        var longResult = subject.AddTopic(longTitle);
        Assert.False(longResult.IsOk);
        Assert.Equal("planning.topic.title_too_long", longResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void UpdateTopic_EdgeCases_HandleNotFoundAndValidationErrors()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;
        var topic = subject.AddTopic("Original").Data;

        var notFound = subject.UpdateTopic(TopicId.New(), "New Title", null);
        Assert.False(notFound.IsOk);
        Assert.Equal("planning.topic.not_found", notFound.Failure.Messages[0].Code);

        var emptyTitle = subject.UpdateTopic(topic.Id, "   ", null);
        Assert.False(emptyTitle.IsOk);
        Assert.Equal("planning.topic.title_required", emptyTitle.Failure.Messages[0].Code);

        var longTitle = new string('t', Subject.MaxTopicTitleLength + 1);
        var longResult = subject.UpdateTopic(topic.Id, longTitle, null);
        Assert.False(longResult.IsOk);
        Assert.Equal("planning.topic.title_too_long", longResult.Failure.Messages[0].Code);
    }

    [Fact]
    public void MoveTopic_EdgeCases_HandleNotFoundSameParentAndMultipleOldSiblings()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;

        var notFound = subject.MoveTopic(TopicId.New(), null);
        Assert.False(notFound.IsOk);
        Assert.Equal("planning.topic.not_found", notFound.Failure.Messages[0].Code);

        var a = subject.AddTopic("A").Data;
        var b = subject.AddTopic("B").Data;
        var c = subject.AddTopic("C").Data;

        var missingParent = subject.MoveTopic(a.Id, TopicId.New());
        Assert.False(missingParent.IsOk);
        Assert.Equal("planning.topic.parent_not_found", missingParent.Failure.Messages[0].Code);

        var sameParent = subject.MoveTopic(a.Id, null);
        Assert.True(sameParent.IsOk);

        var moveAToB = subject.MoveTopic(a.Id, b.Id);
        Assert.True(moveAToB.IsOk);
        Assert.Equal(b.Id, subject.Topics.First(t => t.Id == a.Id).ParentId);
        Assert.Equal(0, subject.Topics.First(t => t.Id == b.Id).Order);
        Assert.Equal(1, subject.Topics.First(t => t.Id == c.Id).Order);
    }

    [Fact]
    public void ReorderTopics_EdgeCases_HandleMissingParentAndInvalidList()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;
        var t1 = subject.AddTopic("T1").Data;

        var missingParent = subject.ReorderTopics(TopicId.New(), []);
        Assert.False(missingParent.IsOk);
        Assert.Equal("planning.topic.parent_not_found", missingParent.Failure.Messages[0].Code);

        var invalidList = subject.ReorderTopics(null, [t1.Id, TopicId.New()]);
        Assert.False(invalidList.IsOk);
        Assert.Equal("planning.topic.invalid_reorder", invalidList.Failure.Messages[0].Code);
    }

    [Fact]
    public void ChangeTopicStatus_WithMissingTopic_ReturnsNotFound()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;
        var result = subject.ChangeTopicStatus(TopicId.New(), TopicStatus.Studied);

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.not_found", result.Failure.Messages[0].Code);
    }

    [Fact]
    public void RemoveTopicSubtree_WithMissingTopic_ReturnsNotFound()
    {
        var subject = Subject.Create(_ownerId, _planId, "Name").Data;
        var result = subject.RemoveTopicSubtree(TopicId.New());

        Assert.False(result.IsOk);
        Assert.Equal("planning.topic.not_found", result.Failure.Messages[0].Code);
    }
}
