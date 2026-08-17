UPDATE [PCs]
SET [LaboratoryId] = (
    SELECT [LaboratoryId]
    FROM [Laboratories]
    WHERE [LabName] = 'ComLab 601'
)
WHERE [PCNumber] IN (
    '601-PC01',
    '601-PC02',
    '601-PC03',
    '601-PC04',
    '601-PC05',
    '601-PC06'
);

SELECT
    PCId,
    PCNumber,
    LaboratoryId,
    Status,
    CurrentUserId,
    LastSeen,
    IsEnabled
FROM [PCs]
ORDER BY PCId;