INSERT INTO dbo.Products (Name, Brand, Category, TargetsCsv, HairTypesCsv, ForChemical, Description)
VALUES
    (N'Shampoo Hidratante Nutritivo', N'HairLab', 'shampoo',      'hydration',                 'all',              1, N'Limpeza suave com ativos de hidratação.'),
    (N'Shampoo Antirresíduos',        N'HairLab', 'shampoo',      'oil_control',               'straight,wavy',    0, N'Remove acúmulo e controla oleosidade.'),
    (N'Condicionador Hidratante',     N'HairLab', 'conditioner',  'hydration,frizz_control',   'all',              1, N'Reidrata e reduz o frizz no desembaraço.'),
    (N'Condicionador Reconstrutor',   N'HairLab', 'conditioner',  'damage_repair',             'all',              1, N'Repõe massa e força para fios danificados.'),
    (N'Máscara de Hidratação Intensa',N'HairLab', 'mask',         'hydration',                 'all',              1, N'Tratamento profundo de hidratação semanal.'),
    (N'Máscara de Reconstrução',      N'HairLab', 'mask',         'damage_repair',             'all',              1, N'Reconstrução com aminoácidos e proteínas.'),
    (N'Máscara Nutritiva para Cachos',N'CurlCo',  'mask',         'hydration,frizz_control',   'curly,coily',      1, N'Nutrição e definição para cabelos cacheados e crespos.'),
    (N'Leave-in Antifrizz',           N'CurlCo',  'leave_in',     'frizz_control,hydration',   'wavy,curly,coily', 1, N'Controle de frizz e umidade sem pesar.'),
    (N'Óleo Capilar Reparador',       N'CurlCo',  'oil',          'frizz_control,damage_repair','curly,coily',     1, N'Selagem de pontas e brilho.'),
    (N'Sérum de Pontas',              N'HairLab', 'oil',          'damage_repair,frizz_control','all',             1, N'Finalização que disciplina e protege as pontas.'),
    (N'Tônico Fortalecedor Antiqueda',N'ScalpMed','tonic',        'hairloss_control',          'all',              0, N'Fortalece o couro cabeludo e reduz a queda.'),
    (N'Shampoo Antioleosidade',       N'ScalpMed','shampoo',      'oil_control',               'straight,wavy',    0, N'Equilibra a oleosidade da raiz.');
